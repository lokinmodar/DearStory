# Static docs output

`dearstory build` emits the Windows-slice static site to
`artifacts/docs`.

## Output

- `index.html` with the merged document/story index;
- one HTML file per Markdown source under the workspace docs root;
- `search-data.json` with the lightweight searchable story index;
- real screenshots copied from the visual capture run, such as
  `buttons-primary.png` and `buttons-primarymanaged.png`;
- `capture-results.json`, copied from the shared visual-capture artifact root so
  the static build and the regression manifest stay aligned.

## Safety boundaries

- only Markdown plus typed DearStory Doc Blocks are parsed;
- no executable MDX or arbitrary JavaScript is evaluated;
- screenshots come from the shared `DearStory.Capture` pipeline, not a
  placeholder image path.
