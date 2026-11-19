using BolaLens.Analysis.DI;
using BolaLens.Analysis.Endpoints;
using BolaLens.Analysis.Guards;
using BolaLens.Analysis.Infrastructure;
using BolaLens.Analysis.Schema;
using BolaLens.Analysis.Sinks;
using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace BolaLens.Analysis.Taint;

/// <summary>Inter-procedural, flow-ordered taint walker over IOperation trees. Starting from one
/// client-controlled id parameter of an endpoint it follows values through locals, DTO properties,
/// arguments → parameters and return values (controller → service → repository, depth-bounded),
/// recognises EF Core loads/writes/commits/bulk operations, caller identity and guard conditions.
/// Input: endpoint + taint source. Limitation: path-insensitive (branches are walked in order and
/// merged); fields/static state are not tracked; summaries are recomputed per call site.</summary>
public sealed class PathWalker
{
    private static readonly string[] FrameworkNamespaces =
        ["System", "Microsoft.Extensions", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Microsoft.Net", "Newtonsoft"];

    private static readonly HashSet<string> MutatorNames =
        ["Map", "ApplyTo", "SetValues", "Populate", "PopulateObject", "Patch", "TryUpdateModelAsync", "CopyTo"];

    private readonly AnalysisContext _context;
    private readonly OwnershipSchema _schema;
    private readonly ImplementationResolver _resolver;
    private readonly CallerIdentityRecognizer _identity;
    private readonly OwnerCandidateInference _owners;
    private readonly int _maxDepth;
    private PathContext _ctx = new();
    private CancellationToken _ct;

    public PathWalker(AnalysisContext context, OwnershipSchema schema, ImplementationResolver resolver)
    {
        _context = context;
        _schema = schema;
        _resolver = resolver;
        _identity = new CallerIdentityRecognizer(context.Config.Identity);
        _owners = new OwnerCandidateInference(context.Config.Ownership);
        _maxDepth = context.Config.Analysis.MaxCallDepth;
    }

    /// <summary>Walks one endpoint for one tainted parameter and returns the raw path state.</summary>
    public PathContext Walk(DiscoveredEndpoint endpoint, ParameterInfo source, CancellationToken ct = default)
    {
        _ctx = new PathContext();
        _ct = ct;
        var action = endpoint.Action;
        var syntax = action.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(ct);
        if (syntax is null)
        {
            _ctx.Unresolved.Add(new UnresolvedItem("action has no source", endpoint.Info.Location));
            return _ctx;
        }

        var args = new AbstractValue[action.Parameters.Length];
        for (var i = 0; i < action.Parameters.Length; i++)
        {
            var p = action.Parameters[i];
            var info = endpoint.Info.Parameters.FirstOrDefault(x => x.Name == p.Name);
            var fromRequest = info is not null && info.Source is not (BindingSource.Service or BindingSource.Unknown);
            args[i] = new AbstractValue { ClientControlled = fromRequest, Tainted = p.Name == source.Name };
            if (p.Name == source.Name)
            {
                var where = p.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(ct) is { } ps ? _context.Ref(ps, snippet: true) : endpoint.Info.Location;
                var idPart = source.IdPropertyPath is null ? "" : $" — id-like members: {source.IdPropertyPath}";
                _ctx.AddStep(StepKind.Source, $"{p.Name} : {source.Type} ({source.Source.ToString().ToLowerInvariant()}, client-controlled){idPart}", Symbols.Display(p), where, 0);
            }
        }

        AnalyseMethod(action, args, AbstractValue.None, typeArgs: null, depth: 0);
        return _ctx;
    }

    // ------------------------------------------------------------------ methods

    private AbstractValue AnalyseMethod(IMethodSymbol method, AbstractValue[] args, AbstractValue receiver,
        IReadOnlyDictionary<string, ITypeSymbol>? typeArgs, int depth)
    {
        var impl = method.PartialImplementationPart ?? method;
        var syntax = impl.DeclaringSyntaxReferences.Select(r => r.GetSyntax(_ct)).FirstOrDefault(HasBody);
        if (syntax is null) return AbstractValue.None;
        var model = _context.GetModel(syntax.SyntaxTree);
        if (model is null)
        {
            _ctx.Unresolved.Add(new UnresolvedItem($"no semantic model for {Symbols.Display(method)}", _context.Ref(syntax)));
            return AbstractValue.None;
        }

        var key = Symbols.Key(method);
        if (!_ctx.CallStack.Add(key)) return AbstractValue.None; // recursion
        _ctx.MethodsAnalysed++;

        var declared = model.GetDeclaredSymbol(syntax, _ct) as IMethodSymbol
                       ?? model.GetSymbolInfo(syntax, _ct).Symbol as IMethodSymbol
                       ?? method;
        var frame = new MethodFrame(declared, depth) { This = receiver };
        for (var i = 0; i < declared.Parameters.Length && i < args.Length; i++)
            frame.Values[declared.Parameters[i]] = args[i] ?? AbstractValue.None;
        if (typeArgs is not null)
            foreach (var kv in typeArgs) frame.TypeArguments[kv.Key] = kv.Value;

        IOperation? body = model.GetOperation(syntax, _ct) switch
        {
            IMethodBodyBaseOperation mb => (IOperation?)mb.BlockBody ?? mb.ExpressionBody,
            ILocalFunctionOperation lf => lf.Body,
            IAnonymousFunctionOperation af => af.Body,
            IDelegateCreationOperation { Target: IAnonymousFunctionOperation af2 } => af2.Body,
            var other => other,
        };
        if (body is not null) WalkStatement(body, frame);

        var end = _ctx.NextSeq();
        foreach (var g in _ctx.Guards.Where(g => ReferenceEquals(g.Frame, frame) && g.UntilFrameEnd && g.ActiveUntil == int.MinValue))
            g.ActiveUntil = end;
        _ctx.CallStack.Remove(key);
        return frame.Return;
    }

    private static bool HasBody(SyntaxNode n) => n switch
    {
        BaseMethodDeclarationSyntax m => m.Body is not null || m.ExpressionBody is not null,
        LocalFunctionStatementSyntax l => l.Body is not null || l.ExpressionBody is not null,
        AccessorDeclarationSyntax a => a.Body is not null || a.ExpressionBody is not null,
        AnonymousFunctionExpressionSyntax => true, // Minimal-API lambda handlers
        _ => false,
    };

    // ------------------------------------------------------------------ statements

    private void WalkStatement(IOperation? op, MethodFrame f)
    {
        if (op is null) return;
        _ct.ThrowIfCancellationRequested();
        switch (op)
        {
            case IBlockOperation b:
                foreach (var s in b.Operations) WalkStatement(s, f);
                break;

            case IVariableDeclarationGroupOperation g:
                foreach (var decl in g.Declarations) Declare(decl, f);
                break;

            case IVariableDeclarationOperation d:
                Declare(d, f);
                break;

            case IExpressionStatementOperation e:
                Eval(e.Operation, f);
                break;

            case IReturnOperation r:
                if (r.ReturnedValue is not null) f.Return = f.Return.Merge(Eval(r.ReturnedValue, f));
                break;

            case IConditionalOperation c:
                HandleIf(c, f);
                break;

            case IForEachLoopOperation fe:
                var coll = Eval(fe.Collection, f);
                var element = coll.Entity is { IsCollection: true } t ? new AbstractValue { Entity = t } : coll.DataOnly();
                foreach (var local in fe.Locals) f.Values[local] = element;
                WalkStatement(fe.Body, f);
                break;

            case IWhileLoopOperation w:
                Eval(w.Condition, f);
                WalkStatement(w.Body, f);
                break;

            case IForLoopOperation fl:
                foreach (var s in fl.Before) WalkStatement(s, f);
                Eval(fl.Condition, f);
                WalkStatement(fl.Body, f);
                foreach (var s in fl.AtLoopBottom) WalkStatement(s, f);
                break;

            case ITryOperation tr:
                WalkStatement(tr.Body, f);
                foreach (var c in tr.Catches) WalkStatement(c.Handler, f);
                WalkStatement(tr.Finally, f);
                break;

            case IUsingOperation u:
                WalkStatement(u.Resources, f);
                WalkStatement(u.Body, f);
                break;

            case IUsingDeclarationOperation ud:
                WalkStatement(ud.DeclarationGroup, f);
                break;

            case ILockOperation l:
                Eval(l.LockedValue, f);
                WalkStatement(l.Body, f);
                break;

            case ISwitchOperation sw:
                Eval(sw.Value, f);
                foreach (var c in sw.Cases)
                    foreach (var s in c.Body) WalkStatement(s, f);
                break;

            case ILabeledOperation lab:
                WalkStatement(lab.Operation, f);
                break;

            case ILocalFunctionOperation:
                break; // analysed only when invoked

            default:
                foreach (var child in op.ChildOperations)
                {
                    if (child.Syntax is StatementSyntax) WalkStatement(child, f);
                    else Eval(child, f);
                }
                break;
        }
    }

    private void Declare(IVariableDeclarationOperation decl, MethodFrame f)
    {
        foreach (var d in decl.Declarators)
        {
            var init = d.Initializer?.Value ?? decl.Initializer?.Value;
            var v = Eval(init, f);
            f.Values[d.Symbol] = v;
            if (v.Entity is { } token) token.VariableName ??= d.Symbol.Name;
            if (v.Identity is { } kind && init is not null && GuardDetector.Unwrap(init) is not ILocalReferenceOperation)
                Step(StepKind.Caller, $"trusted identity ({kind}): {d.Symbol.Name}", null, init, f);
        }
    }

    private void HandleIf(IConditionalOperation c, MethodFrame f)
    {
        Eval(c.Condition, f);
        var stepIndex = Step(StepKind.Check, "condition", null, c, f);
        var start = _ctx.Seq;
        var comparisons = GuardDetector.Collect(c.Condition);
        var trueExit = GuardDetector.Exit(c.WhenTrue);
        var falseExit = GuardDetector.Exit(c.WhenFalse);

        WalkStatement(c.WhenTrue, f);
        var afterTrue = _ctx.NextSeq();
        WalkStatement(c.WhenFalse, f);
        var afterFalse = _ctx.NextSeq();

        var facts = new List<string>();
        foreach (var cmp in comparisons)
        {
            var record = BuildGuard(cmp, c, f, facts);
            if (record is null) continue;

            if (cmp.TrueOnMismatch)
            {
                if (trueExit != ExitKind.None && !cmp.InConjunction)
                    Activate(record, afterTrue, trueExit, f);
                else if (trueExit == ExitKind.None && c.WhenFalse is not null && !cmp.InConjunction)
                    (record.ActiveFrom, record.ActiveUntil) = (afterTrue, afterFalse);
            }
            else
            {
                if (falseExit != ExitKind.None && !cmp.InDisjunction)
                    Activate(record, start, falseExit, f);
                else if (!cmp.InDisjunction)
                    (record.ActiveFrom, record.ActiveUntil) = (start, afterTrue);
            }
            _ctx.Guards.Add(record);
        }

        if (facts.Count > 0 && stepIndex >= 0 && stepIndex < _ctx.Steps.Count)
            _ctx.Steps[stepIndex] = _ctx.Steps[stepIndex] with { Description = string.Join("; ", facts) };
    }

    private static void Activate(GuardRecord g, int from, ExitKind exit, MethodFrame f)
    {
        g.ActiveFrom = from;
        if (exit == ExitKind.Throw) g.ActiveUntil = int.MaxValue;
        // Return-exit: active until the end of this frame; filled in by AnalyseMethod.
    }

    private GuardRecord? BuildGuard(Comparison cmp, IConditionalOperation c, MethodFrame f, List<string> facts)
    {
        var location = _context.StatementRef(c.Syntax);
        var exitKind = cmp.TrueOnMismatch ? GuardDetector.Exit(c.WhenTrue) : GuardDetector.Exit(c.WhenFalse);
        var problem = (cmp.TrueOnMismatch && cmp.InConjunction) || (!cmp.TrueOnMismatch && cmp.InDisjunction)
            ? "check is combined with other conditions and does not always stop the request" : null;

        if (cmp.IsExistsCheck)
        {
            var ev = ValueOf(cmp.Left, f).Exists;
            if (ev is not { KeyTainted: true }) return null;
            facts.Add($"existence check on {ev.EntityType[(ev.EntityType.LastIndexOf('.') + 1)..]} by the client-controlled id"
                      + (ev.OwnerKind is { } ok ? $" and {ev.OwnerProperty} (owner: {ok}) == {ev.Caller} (trusted identity)" : " without an owner condition"));
            return new GuardRecord
            {
                Kind = GuardKind.ExistenceCheck, Token = null, EntityType = ev.EntityType, Location = location, Frame = f,
                OwnerProperty = ev.OwnerProperty, OwnerKind = ev.OwnerKind, IdentityKind = ev.OwnerKind,
                Comparand = ev.OwnerKind is null ? ComparandClass.Other : ComparandClass.Identity,
                CallerExpression = ev.Caller, UntilFrameEnd = exitKind == ExitKind.Return, StructuralProblem = problem,
            };
        }

        if (cmp.IsResourceCheck)
        {
            var rv = ValueOf(cmp.Left, f);
            if (rv.AuthResultFor is not { } token) return null;
            facts.Add($"authorization result for {token.VariableName ?? token.ShortName}");
            return new GuardRecord
            {
                Kind = GuardKind.ResourceAuthorization, Token = token, Location = location, Frame = f,
                Comparand = ComparandClass.Identity, IdentityKind = OwnerKind.User, AuthForCaller = rv.AuthForCaller,
                CallerExpression = "IAuthorizationService.AuthorizeAsync", UntilFrameEnd = exitKind == ExitKind.Return,
                StructuralProblem = problem,
            };
        }

        var left = ValueOf(cmp.Left, f);
        var right = cmp.Right is null ? AbstractValue.None : ValueOf(cmp.Right, f);
        var (owner, other, otherOp) = left.Owner is not null ? (left.Owner, right, cmp.Right) : (right.Owner, left, cmp.Left);
        if (owner is null) return null;

        var comparand = other.Identity is not null ? ComparandClass.Identity
            : other.Tainted || other.ClientControlled ? ComparandClass.ClientControlled
            : ComparandClass.Other;
        var otherText = otherOp is null ? "?" : SourceRefs.Snippet(otherOp.Syntax);
        facts.Add($"{owner.Token.VariableName ?? owner.Token.ShortName}.{owner.Property} (owner: {owner.Kind}) vs {otherText} ({Describe(other)})");

        return new GuardRecord
        {
            Kind = GuardKind.OwnerEquality, Token = owner.Token, OwnerProperty = owner.Property, OwnerKind = owner.Kind,
            Comparand = comparand, IdentityKind = other.Identity, CallerExpression = otherText, Location = location,
            Frame = f, UntilFrameEnd = exitKind == ExitKind.Return, StructuralProblem = problem,
        };
    }

    private static string Describe(AbstractValue v) =>
        v.Identity is { } k ? $"trusted identity: {k}"
        : v.Tainted || v.ClientControlled ? "client-controlled"
        : v.Owner is { } o ? $"owner property of {o.Token.ShortName}"
        : "other";

    // ------------------------------------------------------------------ expressions

    private AbstractValue ValueOf(IOperation op, MethodFrame f)
    {
        if (f.Cache.TryGetValue(op, out var v)) return v;
        var u = GuardDetector.Unwrap(op);
        if (f.Cache.TryGetValue(u, out v)) return v;
        return Peek(u, f);
    }

    private AbstractValue Eval(IOperation? op, MethodFrame f)
    {
        if (op is null) return AbstractValue.None;
        if (f.Cache.TryGetValue(op, out var cached)) return cached;
        var v = EvalCore(op, f);
        f.Cache[op] = v;
        return v;
    }

    private AbstractValue EvalCore(IOperation op, MethodFrame f)
    {
        switch (op)
        {
            case ILiteralOperation or IDefaultValueOperation or ITypeOfOperation or INameOfOperation or ISizeOfOperation
                or IAnonymousFunctionOperation or IDelegateCreationOperation or IDeclarationExpressionOperation:
                return AbstractValue.None;

            case IParameterReferenceOperation p:
                return f.Values.TryGetValue(p.Parameter, out var pv) ? pv : AbstractValue.None;

            case ILocalReferenceOperation l:
                return f.Values.TryGetValue(l.Local, out var lv) ? lv : AbstractValue.None;

            case IInstanceReferenceOperation:
                return f.This;

            case IConversionOperation c:
                return Eval(c.Operand, f);
            case IParenthesizedOperation pa:
                return Eval(pa.Operand, f);
            case IAwaitOperation a:
                return Eval(a.Operation, f);
            case IArgumentOperation arg:
                return Eval(arg.Value, f);

            case ICoalesceOperation co:
                return Eval(co.Value, f).Merge(Eval(co.WhenNull, f));

            case IConditionalAccessOperation ca:
            {
                var inst = Eval(ca.Operation, f);
                f.ConditionalAccess.Push(inst);
                try { return Eval(ca.WhenNotNull, f); }
                finally { f.ConditionalAccess.Pop(); }
            }
            case IConditionalAccessInstanceOperation:
                return f.ConditionalAccess.Count > 0 ? f.ConditionalAccess.Peek() : AbstractValue.None;

            case IConditionalOperation cond when cond.Type is null || cond.Syntax is IfStatementSyntax:
                HandleIf(cond, f);
                return AbstractValue.None;
            case IConditionalOperation ternary:
                Eval(ternary.Condition, f);
                return Eval(ternary.WhenTrue, f).Merge(Eval(ternary.WhenFalse, f));

            case IPropertyReferenceOperation pr:
            {
                var inst = pr.Instance is null ? AbstractValue.None : Eval(pr.Instance, f);
                foreach (var a in pr.Arguments) Eval(a.Value, f);
                return PropertyValue(pr, inst, f);
            }

            case IFieldReferenceOperation fr:
                if (fr.Instance is not null) Eval(fr.Instance, f);
                return AbstractValue.None;

            case IInvocationOperation inv:
                return EvalInvocation(inv, f);

            case IObjectCreationOperation oc:
                return EvalObjectCreation(oc, f);

            case IAnonymousObjectCreationOperation ao:
                return MergeAll(ao.Initializers.Select(i => i is IAssignmentOperation asg ? Eval(asg.Value, f) : Eval(i, f))).DataOnly();

            case ISimpleAssignmentOperation or ICompoundAssignmentOperation or ICoalesceAssignmentOperation:
                return EvalAssignment((IAssignmentOperation)op, f);

            case IIncrementOrDecrementOperation inc:
                MarkTargetMutated(inc.Target, inc, f);
                return AbstractValue.None;

            case IBinaryOperation b:
            {
                var l2 = Eval(b.LeftOperand, f);
                var r2 = Eval(b.RightOperand, f);
                return b.OperatorKind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract or BinaryOperatorKind.Multiply
                    or BinaryOperatorKind.Divide or BinaryOperatorKind.Concatenate or BinaryOperatorKind.Remainder
                    ? l2.Merge(r2).DataOnly()
                    : AbstractValue.None;
            }

            case IUnaryOperation u:
                Eval(u.Operand, f);
                return AbstractValue.None;

            case IIsPatternOperation ip:
                Eval(ip.Value, f);
                return AbstractValue.None;

            case IIsTypeOperation it:
                Eval(it.ValueOperand, f);
                return AbstractValue.None;

            case IThrowOperation th:
                Eval(th.Exception, f);
                return AbstractValue.None;

            case IArrayCreationOperation ac:
                foreach (var d in ac.DimensionSizes) Eval(d, f);
                return ac.Initializer is null ? AbstractValue.None : Eval(ac.Initializer, f);
            case IArrayInitializerOperation ai:
                return MergeAll(ai.ElementValues.Select(e => Eval(e, f)));

            case IInterpolatedStringOperation s:
                return MergeAll(s.Parts.SelectMany(p => p.ChildOperations).Select(x => Eval(x, f))).DataOnly();

            case ITupleOperation t:
                return MergeAll(t.Elements.Select(e => Eval(e, f)));

            case ISwitchExpressionOperation se:
                Eval(se.Value, f);
                return MergeAll(se.Arms.Select(arm => Eval(arm.Value, f)));

            default:
                return MergeAll(op.ChildOperations.Select(c => c.Syntax is StatementSyntax ? Walked(c, f) : Eval(c, f)));
        }
    }

    private AbstractValue Walked(IOperation op, MethodFrame f)
    {
        WalkStatement(op, f);
        return AbstractValue.None;
    }

    private static AbstractValue MergeAll(IEnumerable<AbstractValue> values) =>
        values.Aggregate(AbstractValue.None, (a, b) => a.Merge(b));

    /// <summary>Side-effect-free evaluation, used inside LINQ predicate lambdas and for guard operands.</summary>
    private AbstractValue Peek(IOperation op, MethodFrame f)
    {
        if (f.Cache.TryGetValue(op, out var cached)) return cached;
        switch (op)
        {
            case IConversionOperation c: return Peek(c.Operand, f);
            case IParenthesizedOperation p: return Peek(p.Operand, f);
            case IAwaitOperation a: return Peek(a.Operation, f);
            case IParameterReferenceOperation pr: return f.Values.TryGetValue(pr.Parameter, out var pv) ? pv : AbstractValue.None;
            case ILocalReferenceOperation lr: return f.Values.TryGetValue(lr.Local, out var lv) ? lv : AbstractValue.None;
            case IInstanceReferenceOperation: return f.This;
            case IPropertyReferenceOperation prop:
                return PropertyValue(prop, prop.Instance is null ? AbstractValue.None : Peek(prop.Instance, f), f);
            case ICoalesceOperation co: return Peek(co.Value, f).Merge(Peek(co.WhenNull, f));
            case IInvocationOperation inv:
            {
                var recv = inv.Instance is null ? AbstractValue.None : Peek(inv.Instance, f);
                var args = inv.Arguments.Select(a => Peek(a.Value, f)).ToArray();
                if (inv.Instance is null && inv.TargetMethod.IsExtensionMethod && args.Length > 0) recv = args[0];
                if (_identity.FromInvocation(inv, recv, args) is { } idv) return idv;
                if (IsParseLike(inv.TargetMethod)) return MergeAll(args.Append(recv)).DataOnly();
                return AbstractValue.None;
            }
            default: return AbstractValue.None;
        }
    }

    private AbstractValue PropertyValue(IPropertyReferenceOperation pr, AbstractValue inst, MethodFrame f)
    {
        if (_identity.FromProperty(pr, inst) is { } id) return id;

        if (Symbols.DbSetElement(pr.Type) is { } element)
            return new AbstractValue { Query = new QueryValue(EntityName(element, f), false, false, null, null, null, null) };

        var name = pr.Property.Name;
        var result = AbstractValue.None;

        // a member of an object created in analysed code: exact per-member value
        if (inst.Fields is { } fields)
        {
            var fv = fields.TryGetValue(name, out var v) ? v : AbstractValue.None;
            return inst.ClientControlled && !fv.ClientControlled ? fv with { ClientControlled = true } : fv;
        }

        // owner reached through a navigation: article.Author.PersonId / article.Author?.Username
        if (inst.Owner is { } nav && (name.EndsWith("Id", StringComparison.Ordinal) || name is "Username" or "UserName" or "Email" or "Name"))
            return new AbstractValue { Owner = nav with { Property = $"{nav.Property}.{name}" } };

        if (inst.Entity is { } token)
        {
            // collection navigation of a loaded entity (article.Comments): in-memory LINQ on it selects child entities
            if (pr.Type is INamedTypeSymbol { IsGenericType: true } coll && coll.AllInterfaces.Any(i => i.Name == "IEnumerable")
                && coll.TypeArguments.Length == 1 && _schema.Find(EntityName(coll.TypeArguments[0], f)) is { } child)
                return new AbstractValue { Query = new QueryValue(child.EntityType, false, token.BypassedFilters, null, null, null, null) };
            if (OwnerKindOf(token.EntityType, name) is { } kind)
                result = result.Merge(new AbstractValue { Owner = new OwnerValue(token, name, kind) });
            if (Symbols.InNamespace(pr.Property.ContainingType, "Microsoft.EntityFrameworkCore"))
                result = result.Merge(new AbstractValue { Entity = token }); // entry.CurrentValues etc.
        }
        if (inst.AuthResultFor is not null && name == "Succeeded")
            result = result.Merge(new AbstractValue { AuthResultFor = inst.AuthResultFor, AuthForCaller = inst.AuthForCaller });

        var simpleInstance = pr.Instance?.Type is { } it && Symbols.IsSimple(it);
        if (inst.Tainted && (simpleInstance || _context.IsIdLike(name)))
            result = result.Merge(new AbstractValue { Tainted = true, ClientControlled = true });
        else if (inst.ClientControlled || inst.Tainted)
            result = result.Merge(new AbstractValue { ClientControlled = true });

        return result;
    }

    private AbstractValue EvalAssignment(IAssignmentOperation a, MethodFrame f)
    {
        var v = Eval(a.Value, f);
        switch (a.Target)
        {
            case ILocalReferenceOperation l:
                f.Values[l.Local] = a is ISimpleAssignmentOperation ? v : Get(f, l.Local).Merge(v);
                if (v.Entity is { } t) t.VariableName ??= l.Local.Name;
                if (v.Identity is { } k && GuardDetector.Unwrap(a.Value) is not ILocalReferenceOperation)
                    Step(StepKind.Caller, $"trusted identity ({k}): {l.Local.Name}", null, a, f);
                break;
            case IParameterReferenceOperation p:
                f.Values[p.Parameter] = a is ISimpleAssignmentOperation ? v : Get(f, p.Parameter).Merge(v);
                break;
            default:
                MarkTargetMutated(a.Target, a, f);
                break;
        }
        return v;
    }

    private static AbstractValue Get(MethodFrame f, ISymbol s) => f.Values.TryGetValue(s, out var v) ? v : AbstractValue.None;

    private void MarkTargetMutated(IOperation target, IOperation statement, MethodFrame f)
    {
        switch (target)
        {
            case IPropertyReferenceOperation pr:
            {
                var inst = pr.Instance is null ? AbstractValue.None : Eval(pr.Instance, f);
                if (inst.Entity is { } token)
                {
                    token.Dirty = true;
                    if (token.TaintSelected)
                        Step(StepKind.Mutation, $"modifies {token.VariableName ?? token.ShortName}.{pr.Property.Name}", Symbols.Display(pr.Property), statement, f);
                }
                break;
            }
            case IFieldReferenceOperation fr:
                if (fr.Instance is not null) Eval(fr.Instance, f);
                break;
            default:
                Eval(target, f);
                break;
        }
    }

    private AbstractValue EvalObjectCreation(IObjectCreationOperation oc, MethodFrame f)
    {
        var args = oc.Arguments.Select(a => Eval(a.Value, f)).ToList();
        var type = oc.Type;
        var entityName = type is null ? null : EntityName(type, f);
        var entity = entityName is null ? null : _schema.Find(entityName);

        var keyTainted = false;
        var fields = new Dictionary<string, AbstractValue>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < oc.Arguments.Length; i++)
            if (oc.Arguments[i].Parameter?.Name is { } pn && args[i].IsRelevant) fields[pn] = args[i].DataOnly(); // positional records: ctor param = property
        if (oc.Initializer is not null)
        {
            foreach (var init in oc.Initializer.Initializers)
            {
                if (init is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation tp } sa)
                {
                    var v = Eval(sa.Value, f);
                    if (v.IsRelevant) fields[tp.Property.Name] = v.DataOnly();
                    if (v.Tainted && entity is not null && IsKey(entity, tp.Property.Name)) keyTainted = true;
                }
                else
                {
                    Eval(init, f);
                }
            }
        }

        if (entity is null)
        {
            var data = MergeAll(fields.Values);
            return new AbstractValue { Tainted = data.Tainted, ClientControlled = data.ClientControlled, Fields = fields };
        }

        keyTainted |= args.Any(a => a.Tainted);
        var token = new EntityToken(entity.EntityType, "new", _context.StatementRef(oc.Syntax))
        {
            TaintSelected = keyTainted,
            Detached = true,
        };
        _ctx.Tokens.Add(token);
        if (keyTainted)
            Step(StepKind.Load, $"constructs detached {token.ShortName} with client-controlled key", Symbols.Display(type!), oc, f);
        return new AbstractValue { Entity = token };
    }

    private static bool IsKey(EntityModel entity, string property) =>
        entity.KeyProperties.Contains(property) || property == "Id";

    // ------------------------------------------------------------------ invocations

    private AbstractValue EvalInvocation(IInvocationOperation inv, MethodFrame f)
    {
        var m = inv.TargetMethod;
        var recv = inv.Instance is null ? AbstractValue.None : Eval(inv.Instance, f);
        var args = new AbstractValue[inv.Arguments.Length];
        for (var i = 0; i < inv.Arguments.Length; i++)
        {
            var value = inv.Arguments[i].Value;
            args[i] = GuardDetector.Unwrap(value) is IAnonymousFunctionOperation or IDelegateCreationOperation
                ? AbstractValue.None
                : Eval(value, f);
        }
        var isExtensionCall = inv.Instance is null && m.IsExtensionMethod && args.Length > 0;
        if (isExtensionCall) recv = args[0];

        var category = EfOperationClassifier.Classify(m);
        if (category != EfCategory.None && (!EfOperationClassifier.IsEnumerable(m) || recv.Query is not null))
            return EvalEf(inv, category, recv, args, isExtensionCall, f);

        if (m.Name == "AuthorizeAsync" && (m.ContainingType.Name is "IAuthorizationService" or "AuthorizationServiceExtensions"))
            return EvalAuthorize(inv, args, f);

        if (_identity.FromInvocation(inv, recv, args) is { } identity)
            return identity;

        if (_context.Config.Analysis.DispatchMethods.Contains(m.Name) && TryDispatch(inv, args, isExtensionCall, f) is { } dispatched)
            return dispatched;

        var sourceTarget = Symbols.IsInSource(m.ReducedFrom ?? m) || m.MethodKind == MethodKind.LocalFunction
            || (m.ContainingType is { } ct && Symbols.IsInSource(ct));
        if (sourceTarget)
            return Descend(inv, m, recv, args, f);

        // ---- library call
        if (MutatorNames.Contains(m.Name))
        {
            foreach (var t in args.Append(recv).Select(a => a.Entity).Where(t => t is not null))
            {
                t!.Dirty = true;
                if (t.TaintSelected) Step(StepKind.Mutation, $"{m.Name}(...) modifies {t.VariableName ?? t.ShortName}", Symbols.Display(m), inv, f);
            }
        }
        if (IsParseLike(m))
            return MergeAll(args.Append(recv)).DataOnly();

        var dispatch = m.ContainingType?.TypeKind == TypeKind.Interface || m.IsAbstract;
        if (dispatch && !IsFramework(m) && (args.Any(IsTracked) || IsTracked(recv)))
        {
            RecordUnresolved($"external dispatch: {Symbols.Display(m)} receives the tracked id", inv, f, tainted: true);
        }
        return AbstractValue.None;
    }

    /// <summary>Mediator/CQRS dispatch: sender.Send(command) → the source handler whose Handle method takes the
    /// command's type. Null when no argument has a source type with handlers (falls through to normal handling).</summary>
    private AbstractValue? TryDispatch(IInvocationOperation inv, AbstractValue[] args, bool extensionCall, MethodFrame f)
    {
        for (var i = extensionCall ? 1 : 0; i < inv.Arguments.Length; i++)
        {
            var raw = inv.Arguments[i].Value;
            while (raw is IConversionOperation c) raw = c.Operand;
            if (raw.Type is not INamedTypeSymbol t || !Symbols.IsInSource(t)) continue;
            var handlers = _resolver.FindHandlers(t);
            if (handlers.Count == 0) continue;
            var tracked = IsTracked(args[i]) || args[i].Fields?.Values.Any(IsTracked) == true;
            if (handlers.Count > 1)
            {
                RecordUnresolved($"ambiguous dispatch: {handlers.Count} handlers for {t.Name} ({string.Join(", ", handlers.Select(h => h.ContainingType.Name))})", inv, f, tracked);
                return AbstractValue.None;
            }
            var target = handlers[0];
            if (f.Depth + 1 > _maxDepth)
            {
                if (tracked) _ctx.Truncated = true;
                RecordUnresolved($"max call depth ({_maxDepth}) reached before {target.ContainingType.Name}.{target.Name}", inv, f, tracked);
                return AbstractValue.None;
            }
            Step(StepKind.Call, $"dispatches {t.Name} to {target.ContainingType.Name}.{target.Name}", Symbols.Display(target), inv, f);
            var mapped = Enumerable.Repeat(AbstractValue.None, Math.Max(target.Parameters.Length, 1)).ToArray();
            mapped[0] = args[i];
            return AnalyseMethod(target, mapped, AbstractValue.None, typeArgs: null, f.Depth + 1);
        }
        return null;
    }

    private static bool IsTracked(AbstractValue v) => v.Tainted || v.Entity?.TaintSelected == true;

    private static bool IsFramework(IMethodSymbol m) =>
        FrameworkNamespaces.Any(ns => Symbols.InNamespace(m, ns));

    private bool IsParseLike(IMethodSymbol m) =>
        _context.Config.Analysis.ParseLikeMethods.Contains(m.Name)
        || (m.ContainingType?.SpecialType == SpecialType.System_String && !m.ReturnsVoid)
        || m.ContainingType?.Name is "Convert" or "Guid";

    private AbstractValue EvalAuthorize(IInvocationOperation inv, AbstractValue[] args, MethodFrame f)
    {
        EntityToken? token = null;
        var forCaller = false;
        for (var i = 0; i < inv.Arguments.Length; i++)
        {
            var pname = inv.Arguments[i].Parameter?.Name;
            if (pname == "resource") token = args[i].Entity;
            if (pname == "user") forCaller = args[i].IsPrincipal;
        }
        Step(StepKind.Call, "resource-based authorization call", Symbols.Display(inv.TargetMethod), inv, f);
        return token is null ? AbstractValue.None : new AbstractValue { AuthResultFor = token, AuthForCaller = forCaller };
    }

    private AbstractValue Descend(IInvocationOperation inv, IMethodSymbol m, AbstractValue recv, AbstractValue[] args, MethodFrame f)
    {
        var relevant = args.Any(a => a.IsRelevant) || recv.Entity is not null;
        var tracked = args.Any(IsTracked) || IsTracked(recv);
        var resolution = _resolver.Resolve(m);
        if (resolution is { Kind: ResolutionKind.LibraryImplementation, Target: { } lib })
        {
            // an application interface implemented by EF Core itself (IApplicationDbContext.SaveChangesAsync, .Set<T>() …)
            var libCategory = EfOperationClassifier.Classify(lib);
            return libCategory != EfCategory.None
                ? EvalEf(inv, libCategory, recv, args, inv.Instance is null && m.IsExtensionMethod, f)
                : AbstractValue.None;
        }
        if (resolution.Kind != ResolutionKind.Resolved || resolution.Target is null)
        {
            if (relevant || resolution.Kind == ResolutionKind.Ambiguous)
                RecordUnresolved(resolution.Reason, inv, f, tracked);
            return AbstractValue.None;
        }

        if (f.Depth + 1 > _maxDepth)
        {
            if (tracked) _ctx.Truncated = true;
            RecordUnresolved($"max call depth ({_maxDepth}) reached before {Symbols.Display(m)}", inv, f, tracked);
            return AbstractValue.None;
        }

        var target = resolution.Target;
        var stepsBefore = _ctx.Steps.Count;
        var callStep = Step(StepKind.Call, $"calls {target.ContainingType.Name}.{target.Name}", Symbols.Display(target), inv, f);

        var mapped = new AbstractValue[Math.Max(target.Parameters.Length, 1)];
        for (var i = 0; i < mapped.Length; i++) mapped[i] = AbstractValue.None;
        for (var i = 0; i < inv.Arguments.Length; i++)
        {
            var ordinal = inv.Arguments[i].Parameter?.Ordinal ?? i;
            if (ordinal < mapped.Length) mapped[ordinal] = mapped[ordinal].Merge(args[i]);
        }

        var result = AnalyseMethod(target, mapped, recv, TypeArguments(m, target, f), f.Depth + 1);

        // drop call steps that led nowhere and carried nothing interesting
        if (!relevant && _ctx.Steps.Count == stepsBefore + 1 && callStep == stepsBefore)
            _ctx.Steps.RemoveAt(callStep);
        return result;
    }

    private static Dictionary<string, ITypeSymbol> TypeArguments(IMethodSymbol call, IMethodSymbol target, MethodFrame f)
    {
        var map = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);
        ITypeSymbol Sub(ITypeSymbol t) => t is ITypeParameterSymbol tp && f.TypeArguments.TryGetValue(tp.Name, out var s) ? s : t;

        var callArgs = call.ContainingType?.TypeArguments ?? [];
        var targetParams = target.ContainingType?.TypeParameters ?? [];
        for (var i = 0; i < Math.Min(callArgs.Length, targetParams.Length); i++) map[targetParams[i].Name] = Sub(callArgs[i]);
        for (var i = 0; i < Math.Min(call.TypeArguments.Length, target.TypeParameters.Length); i++) map[target.TypeParameters[i].Name] = Sub(call.TypeArguments[i]);
        return map;
    }

    private void RecordUnresolved(string reason, IOperation op, MethodFrame f, bool tainted)
    {
        var location = _context.StatementRef(op.Syntax);
        _ctx.Unresolved.Add(new UnresolvedItem(reason, location));
        Step(StepKind.Unresolved, reason, null, op, f);
        if (tainted) _ctx.TaintEnteredUnresolved = true;
    }

    // ------------------------------------------------------------------ EF Core

    private AbstractValue EvalEf(IInvocationOperation inv, EfCategory category, AbstractValue recv, AbstractValue[] args, bool extensionCall, MethodFrame f)
    {
        var m = inv.TargetMethod;
        var first = extensionCall ? 1 : 0;
        switch (category)
        {
            case EfCategory.SetAccess:
                return new AbstractValue { Query = new QueryValue(EntityName(m.TypeArguments[0], f), false, false, null, null, null, null) };

            case EfCategory.Chain:
            {
                if (recv.Query is null) return recv;
                var q = recv.Query;
                if (m.Name == "Where") q = ApplyPredicates(q, inv, first, f);
                return recv with { Query = q };
            }

            case EfCategory.Bypass:
                return recv.Query is null ? recv : recv with { Query = recv.Query with { BypassedFilters = true } };

            case EfCategory.Load:
            {
                var q = recv.Query;
                if (q is null && m.Name.StartsWith("Find", StringComparison.Ordinal) && m.TypeArguments.Length == 1)
                    q = new QueryValue(EntityName(m.TypeArguments[0], f), false, false, null, null, null, null); // DbContext.Find<T>
                if (q is null) return recv.Entity is { IsCollection: true } coll ? new AbstractValue { Entity = coll } : AbstractValue.None;

                if (m.Name.StartsWith("Find", StringComparison.Ordinal))
                    q = q with { KeyTainted = q.KeyTainted || args.Skip(first).Any(a => a.Tainted) };
                else
                    q = ApplyPredicates(q, inv, first, f);
                var token = TokenFrom(q, "load", inv, collection: false);
                var how = token.TaintSelected ? " selected by the client-controlled id" : "";
                var bypass = token.BypassedFilters ? " (IgnoreQueryFilters)" : "";
                Step(StepKind.Load, $"loads {token.ShortName}{how}{bypass}", Symbols.Display(m), inv, f);
                if (token.TaintSelected && _context.Config.Analysis.IncludeReadOnlyPaths) AddSink(SinkKind.Read, token, inv, f);
                return new AbstractValue { Entity = token };
            }

            case EfCategory.Materialize:
            {
                if (recv.Query is null) return recv;
                var token = TokenFrom(recv.Query, "list", inv, collection: true);
                Step(StepKind.Load, $"loads {token.ShortName} list{(token.TaintSelected ? " selected by the client-controlled id" : "")}", Symbols.Display(m), inv, f);
                if (token.TaintSelected && _context.Config.Analysis.IncludeReadOnlyPaths) AddSink(SinkKind.Read, token, inv, f);
                return new AbstractValue { Entity = token };
            }

            case EfCategory.Exists:
            {
                if (recv.Query is null || !m.Name.StartsWith("Any", StringComparison.Ordinal)) return AbstractValue.None;
                var q = ApplyPredicates(recv.Query, inv, first, f);
                return new AbstractValue { Exists = new ExistsCheck(q.EntityType, q.KeyTainted, q.OwnerPredicateKind, q.OwnerPredicateProperty, q.OwnerPredicateCaller) };
            }

            case EfCategory.Write:
            {
                var target = args.Skip(first).Select(a => a.Entity).FirstOrDefault(t => t is not null)
                             ?? (extensionCall ? null : null);
                if (target is null)
                {
                    if (args.Any(a => a.Tainted))
                        RecordUnresolved($"{m.Name} on an entity not traced to a load", inv, f, tainted: true);
                    return AbstractValue.None;
                }
                var kind = m.Name.StartsWith("Remove", StringComparison.Ordinal) ? SinkKind.Remove
                    : m.Name.StartsWith("Attach", StringComparison.Ordinal) ? SinkKind.Attach
                    : SinkKind.Update;
                if (target.TaintSelected) AddSink(kind, target, inv, f);
                return AbstractValue.None;
            }

            case EfCategory.Create:
                return AbstractValue.None; // creating new objects is out of primary scope

            case EfCategory.Commit:
            {
                Step(StepKind.Commit, "commits tracked changes", Symbols.Display(m), inv, f);
                foreach (var t in _ctx.Tokens.Where(t => t.Dirty).ToList())
                {
                    if (t.TaintSelected) AddSink(SinkKind.SaveChanges, t, inv, f);
                    t.Dirty = false;
                }
                return AbstractValue.None;
            }

            case EfCategory.BulkUpdate or EfCategory.BulkDelete:
            {
                if (recv.Query is null) return AbstractValue.None;
                var token = TokenFrom(recv.Query, "bulk", inv, collection: true);
                if (token.TaintSelected)
                    AddSink(category == EfCategory.BulkUpdate ? SinkKind.BulkUpdate : SinkKind.BulkDelete, token, inv, f);
                return AbstractValue.None;
            }

            case EfCategory.EntryAccess:
            {
                var t = args.Skip(first).Select(a => a.Entity).FirstOrDefault(x => x is not null) ?? recv.Entity;
                return t is null ? AbstractValue.None : new AbstractValue { Entity = t };
            }
        }
        return AbstractValue.None;
    }

    private EntityToken TokenFrom(QueryValue q, string origin, IOperation op, bool collection)
    {
        var token = new EntityToken(q.EntityType, origin, _context.StatementRef(op.Syntax))
        {
            TaintSelected = q.KeyTainted,
            BypassedFilters = q.BypassedFilters,
            OwnerPredicateKind = q.OwnerPredicateKind,
            OwnerPredicateProperty = q.OwnerPredicateProperty,
            OwnerPredicateCaller = q.OwnerPredicateCaller,
            OwnerPredicateLocation = q.PredicateLocation,
            IsCollection = collection,
        };
        _ctx.Tokens.Add(token);
        return token;
    }

    private void AddSink(SinkKind kind, EntityToken token, IOperation op, MethodFrame f)
    {
        var location = _context.StatementRef(op.Syntax);
        var seq = _ctx.NextSeq();
        _ctx.Sinks.Add(new SinkRecord(kind, token, Symbols.Display(((IInvocationOperation)op).TargetMethod), location, seq, IsStateChanging: kind != SinkKind.Read));
        Step(StepKind.Sink, $"state change: {kind} {token.ShortName}", Symbols.Display(((IInvocationOperation)op).TargetMethod), op, f);
    }

    /// <summary>Analyses predicate lambdas: an equality between an entity property and the tracked id
    /// selects the object; an equality (under &amp;&amp; only) between an owner property and the caller identity
    /// is an ownership predicate.</summary>
    private QueryValue ApplyPredicates(QueryValue q, IInvocationOperation inv, int first, MethodFrame f)
    {
        foreach (var arg in inv.Arguments.Skip(first))
        {
            var lambda = arg.Value.DescendantsAndSelf().OfType<IAnonymousFunctionOperation>().FirstOrDefault();
            if (lambda is null || lambda.Symbol.Parameters.Length == 0) continue;
            var parameter = lambda.Symbol.Parameters[0];
            foreach (var cmp in GuardDetector.Collect(LambdaExpression(lambda)))
            {
                if (cmp.IsResourceCheck || cmp.Right is null || cmp.TrueOnMismatch) continue;
                foreach (var (x, y) in new[] { (cmp.Left, cmp.Right), (cmp.Right, cmp.Left) })
                {
                    if (RootedMember(GuardDetector.Unwrap(x), parameter) is not { } chain) continue;
                    var yv = Peek(GuardDetector.Unwrap(y), f);
                    if (yv.Tainted) q = q with { KeyTainted = true };
                    if (!cmp.InDisjunction && yv.Identity is { } idKind && OwnerKindOf(q.EntityType, chain.Top) is { } ownerKind
                        && idKind == ownerKind && (q.OwnerPredicateKind is null || ownerKind == OwnerKind.User))
                    {
                        q = q with
                        {
                            OwnerPredicateKind = ownerKind,
                            OwnerPredicateProperty = chain.Path,
                            OwnerPredicateCaller = SourceRefs.Snippet(y.Syntax),
                            PredicateLocation = _context.StatementRef(inv.Syntax),
                        };
                    }
                }
            }
        }
        return q;
    }

    /// <summary>For x.A or x.A.B (x = the lambda parameter): the top-level property A and the dotted path.</summary>
    private static (string Top, string Path)? RootedMember(IOperation op, IParameterSymbol parameter)
    {
        var names = new List<string>();
        for (var cur = op; ;)
        {
            switch (cur)
            {
                case IPropertyReferenceOperation pr:
                    names.Insert(0, pr.Property.Name);
                    cur = pr.Instance!;
                    continue;
                case IConditionalAccessInstanceOperation:
                    return null;
                case IParameterReferenceOperation p when SymbolEqualityComparer.Default.Equals(p.Parameter, parameter) && names.Count > 0:
                    return (names[0], string.Join(".", names));
                default:
                    return null;
            }
        }
    }

    private static IOperation LambdaExpression(IAnonymousFunctionOperation lambda) =>
        lambda.Body.Operations is [IReturnOperation { ReturnedValue: { } rv }] ? rv : lambda.Body;

    // ------------------------------------------------------------------ helpers

    private string EntityName(ITypeSymbol type, MethodFrame f)
    {
        if (type is ITypeParameterSymbol tp && f.TypeArguments.TryGetValue(tp.Name, out var sub)) type = sub;
        return type.OriginalDefinition.ToDisplayString();
    }

    private OwnerKind? OwnerKindOf(string entityType, string property)
    {
        var entity = _schema.Find(entityType);
        if (entity is not null)
            return entity.OwnerCandidates.FirstOrDefault(c => c.Property == property)?.Kind
                   ?? entity.OwnerCandidates.FirstOrDefault(c => c.Property == property + "Id")?.Kind; // navigation of an owner FK (Author ↔ AuthorId)
        return _owners.KindOfName(property);
    }

    private int Step(StepKind kind, string description, string? symbol, IOperation op, MethodFrame f) =>
        _ctx.AddStep(kind, description, symbol, _context.StatementRef(op.Syntax), f.Depth);
}
