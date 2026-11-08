# Third-party notices

Files from other projects that are kept in this repository as they were published (vendored), with where each came
from and its SHA-256 as kept here (line endings normalised to LF, which `.gitattributes` keeps for `*.min.js`). Each
folder holds the project's own licence file. NuGet dependencies are not listed here: their licences are in their
packages.

| File | Project and version | Source | Licence | SHA-256 |
|---|---|---|---|---|
| `src/Devoctomy.Blazwiz.Browser/wwwroot/lib/simplewebauthn/index.umd.min.js` | `@simplewebauthn/browser` 14.0.0 | `https://unpkg.com/@simplewebauthn/browser@14.0.0/dist/bundle/index.umd.min.js` | MIT, Matthew Miller (`LICENSE.md` beside it) | `29bb0699b45bc39a350d992a13bfc1a4d40d89531390581e1f57f1142c6e221d` (matches the published file) |
| `src/Devoctomy.Blazwiz.UI/wwwroot/lib/bootstrap/css/bootstrap.min.css` | Bootstrap 5.3.0 | `https://unpkg.com/bootstrap@5.3.0/dist/css/bootstrap.min.css` | MIT, The Bootstrap Authors (`LICENSE` beside it) | `7f1d37f0d90b6385354c2ac10e2bb91563c46bd7a266ed351222ebcac8496c2a` |
| `src/Devoctomy.Blazwiz.UI/wwwroot/lib/bootstrap-icons/font/bootstrap-icons.min.css` and `fonts/` | Bootstrap Icons 1.10.5 | `https://unpkg.com/bootstrap-icons@1.10.5/font/` | MIT, The Bootstrap Authors (`LICENSE` beside it) | `42cc7996b4ad1d9c91f5112a85417c890b7bdd7d3a73c2c62143f3a4e87046b2` |
| `src/Devoctomy.Blazwiz.UI/wwwroot/lib/inter/` | `@fontsource-variable/inter` 5.3.0 (`wght.css`, family renamed to `Inter`) | `https://unpkg.com/@fontsource-variable/inter@5.3.0/` | SIL Open Font License 1.1, The Inter Project Authors (`LICENSE` beside it) | see `inter.css` |

To check a file against its source, download the file at the URL above and compare the SHA-256 of both with line
endings normalised, for example:

```powershell
(Get-FileHash -Algorithm SHA256 -InputStream ([IO.MemoryStream]::new([Text.Encoding]::UTF8.GetBytes((Get-Content -Raw $path) -replace "`r`n", "`n")))).Hash
```

When a vendored file is updated, update its row here and the licence file beside it.
