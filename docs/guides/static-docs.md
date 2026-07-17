# Static docs output

`dearstory build` currently emits the Windows-slice static site to
`artifacts/docs`.

## Output today

- `index.html` with the merged document/story index;
- one HTML file per Markdown source under the workspace docs root;
- `search-data.json` with the lightweight searchable story index;
- deterministic placeholder screenshots such as `buttons-primary.png`.

## Safety boundaries

- only Markdown plus typed DearStory Doc Blocks are parsed;
- no executable MDX or arbitrary JavaScript is evaluated;
- screenshots are deterministic artifacts written by the pinned capture worker
  baseline.
