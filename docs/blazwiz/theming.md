# Theming

blazwiz is styled with [Bootstrap 5.3](https://getbootstrap.com/docs/5.3/), the [Inter](https://rsms.me/inter/) font
and [Bootstrap Icons](https://icons.getbootstrap.com/), all shipped in `Devoctomy.Blazwiz.UI`. On top, the framework's
own stylesheet (`_content/Devoctomy.Blazwiz.UI/css/blazwiz.css`) defines its look through **CSS variables** (named
values you can change in one place), all starting `--bw-`.

## Light and dark

The app has a light and a dark theme, using Bootstrap's colour modes: the `data-bs-theme` attribute on `<html>` is
`light` or `dark`.

- The user switches with the toggle in the top bar, or on the Settings page (Appearance).
- Their choice is kept on the device (`UserSettings.Theme`). Until they choose, the operating system's preference is
  used.
- A small script in `index.html` applies the saved theme before Blazor starts, so the page never flashes the wrong
  colours. It reads the setting from local storage, using the app's prefix from `data-app-name` on `<html>`. Keep
  that attribute equal to `AppIdentity.Name`.

In code, inject `IThemeService` (`CurrentTheme`, `IsDark`, `SetThemeAsync`, `ToggleThemeAsync`, and the `ThemeChanged`
event). The theme names are in `ThemeNames` (`ThemeNames.Light`, `ThemeNames.Dark`).

## Re-branding

Your styles go in `wwwroot/css/app.css`, which `index.html` loads **after** the framework's. Override the `--bw-*`
variables there. For example, a teal accent:

```css
:root,
[data-bs-theme="light"] {
    --bw-accent: #0d9488;
    --bw-accent-rgb: 13, 148, 136;
    --bw-accent-2: #14b8a6;
}

[data-bs-theme="dark"] {
    --bw-accent: #2dd4bf;
    --bw-accent-rgb: 45, 212, 191;
    --bw-accent-2: #5eead4;
}
```

Bootstrap's primary colour follows `--bw-accent`, so buttons, links and highlights change with it. Set
`--bw-accent-rgb` to the same colour as three numbers: it is used for see-through tints.

The variables:

| Variable | What it colours or sizes |
|---|---|
| `--bw-accent`, `--bw-accent-rgb` | The main brand colour (Bootstrap's primary) |
| `--bw-accent-2` | The second colour of the brand gradient |
| `--bw-gradient` | The brand gradient, made from the two accents |
| `--bw-bg` | The page background |
| `--bw-surface`, `--bw-surface-2` | Panels and cards, and a slightly different shade for inside them |
| `--bw-sidebar-bg`, `--bw-topbar-bg` | The sidebar and the top bar |
| `--bw-border` | Borders |
| `--bw-text`, `--bw-text-muted` | Text, and quieter text |
| `--bw-hover`, `--bw-active` | Hovered and selected items |
| `--bw-shadow`, `--bw-shadow-lg` | Shadows |
| `--bw-radius`, `--bw-radius-sm` | Corner rounding |
| `--bw-sidebar-width`, `--bw-sidebar-collapsed-width`, `--bw-topbar-height` | Shell sizes |
| `--bw-transition` | Animation timing |

Colours are set separately for light and dark (`:root, [data-bs-theme="light"]` and `[data-bs-theme="dark"]`); sizes
once, on `:root`.

Don't edit the framework's stylesheet: it is replaced on every upgrade. Treat these variables as the theme's public
surface.

## Logo, title and icons

- **Logo**: `AppSettings.LogoUrl` (in `wwwroot/appsettings.json`), shown in the sidebar and on the sign-in pages. It
  defaults to `images/icon.svg`; replace that file or point the setting at another image.
- **Title**: `AppSettings.ApplicationTitle`, shown in the sidebar, on the sign-in page and in the browser tab.
- **App icons and colour**: `wwwroot/favicon.svg`, the PNG icons, `manifest.webmanifest` (the installed app's name,
  icons and colours) and the `theme-color` meta tag in `index.html`.

## Building blocks

The framework styles a few classes you can use in your pages, such as `panel`, `panel-title`, `panel-text`,
`section-title` and `detail-list`, along with Bootstrap's own (`btn`, `alert`, `badge`, `form-control`, `table` ...).
The template's Showcase page (created with `--showcase`) shows them together, ready to copy. Delete it when you no
longer need it.

Modules bring their own styles: the sync module adds `_content/Devoctomy.Blazwiz.UI.Sync/css/blazwiz-sync.css` when
the app starts, just before your `css/app.css`, so your styles still come last. It finds your stylesheet by its link,
`<link href="css/app.css" rel="stylesheet" />`, so keep that link in `index.html` as it is.
