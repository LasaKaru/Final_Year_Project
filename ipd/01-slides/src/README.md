# Rebuilding the slide deck

`build-deck.js` generates `../BolaLens-IPD.pptx` with [PptxGenJS](https://gitbrent.github.io/PptxGenJS/): one theme (Cambria
headings, Calibri body), three layouts (title, content, section), sections, native charts and speaker notes. Diagrams and screenshots
are read from `ipd/diagrams` and `ipd/screenshots`.

```bash
mkdir -p /tmp/deck && (cd /tmp/deck && npm install pptxgenjs@3.12.0)
NODE_PATH=/tmp/deck/node_modules node ipd/01-slides/src/build-deck.js
```

Optional second argument: a module exporting `applyTheme(pptxPath, THEME)` that writes the theme colours into the file (otherwise
PowerPoint shows its stock palette for scheme colours). Easiest workflow for small text changes: edit the `.pptx` directly in
PowerPoint — the generator is only needed for structural changes. Export the PDF from PowerPoint (File → Export → PDF).
