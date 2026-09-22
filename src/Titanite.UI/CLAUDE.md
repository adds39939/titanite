# Titanite.UI

Razor class library holding every component. It is rendered by `Titanite.App`, a Photino
desktop shell — not a web app. Routing is client-side only: there is no server and no browser
chrome, so a bad route shows the `NotFound` page rather than an HTTP error.

```
App.razor(.css)               Router. Root component — no page markup belongs here.
Layout/MainLayout.razor       Shell: titlebar, nav tabs, and the @Body pages render into.
Pages/*.razor                 Routable pages. Exactly one @page directive each.
Components/<Area>/*.razor     Reusable components, grouped by area. Never routable.
Components/<Area>/_Imports.razor  The areas this one may render. Absent means Controls and Icons only.
wwwroot/app.css               Global styles ONLY (see below).
wwwroot/index.html            Document shell. Stylesheet links live here.
```

## Routing

A page is a `.razor` file in `Pages/` with a `@page` directive. Anything without one goes in
`Components/` — being reusable and being routable are different jobs, and mixing them makes a
component impossible to drop onto a second page later.

**Adding a page is two edits:** the `@page` directive, and an entry in the `NavItems` list in
`Layout/MainLayout.razor.cs`, which is what draws the tab bar. A route that exists without a nav
entry is reachable but invisible.

`Pages/Game.razor` is the deliberate exception: `/library/{Launcher}/{Id}` is reached by picking a
game rather than from the tab bar, so it has no `NavItems` entry of its own. A detail page about
one thing is not a place the tab bar can send someone. Both segments are strings, because a
`GameId` is a launcher and an identifier and only Steam's identifiers are numbers — deciding
whether the pair names an installed game belongs to the component rather than the route.

It still lights the Library tab. `NavLink` compares one address and cannot speak for a section
spread over two, so a `NavItem` may name a `Section` — a path prefix it also owns — and
`MainLayout` adds the same `active` class `NavLink` would. Library is at `/` with `Section` of
`library`, which is why the game route sits under `/library/` rather than a `/game/` of its own.

The root route `/` needs `NavLinkMatch.All`; with the default `Prefix` its tab stays highlighted
on every other page.

Keep pages thin. `Pages/Home.razor` is a `@page` directive and `<GameLibrary/>` — the work lives
in the component, which keeps it testable and reusable. A page taking a route parameter stays just
as thin: `Pages/Game.razor` declares the parameter in its code-behind and hands it straight to
`GameConfigPanel`, which does the looking up and owns every state the answer can take.

`Router.NotFound` is obsolete in .NET 10. Use `NotFoundPage="@typeof(NotFound)"`, which takes a
page type rather than inline content. `App.razor` names `MainLayout` and `NotFound` in full,
because the root `_Imports.razor` deliberately carries neither `Pages` nor `Layout`.

## Components

Components are grouped by what they are responsible for, not by what they are:

```
Components/Icons/          One SVG each, no parameters — ChevronIcon, FilterIcon, GridIcon,
                           ListIcon.
Components/Controls/       Shared primitives every area uses — Button, SingleSelectDropdown,
                           MultiSelectDropdown, CollapsibleGroup.
Components/Game/           One game's presentation primitives — GameArtwork, GameTags.
Components/SettingControls/One control per setting kind — SettingEditor and the editors it
                           dispatches to. Rendered only from inside LaunchOptionsEditor.
Components/Launch/         Editing launch options — LaunchOptionsEditor, LaunchOptionsPreview,
                           SaveConfirmation.
Components/Library/        Browsing the collection — GameLibrary and its cards.
Components/GameConfig/     One game's screen — GameConfigPanel and the dialog only it opens.
Components/Presets/        The presets screen — PresetsPanel and the dialog only it opens.
Components/Preferences/    Titanite's own settings — SettingsPanel, VariableDescriptionsPanel.
Components/Proton/         The installed compatibility builds — ProtonPanel.
```

The areas form a hierarchy, and an area may only render the areas below it: pages render the
panel areas (`Library`, `GameConfig`, `Presets`, `Preferences`, `Proton`), those render
`Launch`, `Launch` renders `SettingControls`, and anything may render `Controls` and `Icons`.

**`_Imports.razor` is the guardrail, and it is hierarchical.** The root file carries the framework,
the domain namespaces and `Controls` and `Icons` — what every component may reach. Each area that
reaches further declares that reach in its own `_Imports.razor`, so a sideways or upward reference
does not compile until someone writes it down. Do not add an area to the root file.

**An area folder shadows a type of the same name for the whole `Components` tree.** C# resolves a
name against the enclosing namespaces before it looks at `using` directives, so a folder named
`LaunchOptions/` would make `Titanite.UI.Components.LaunchOptions` win over
`Titanite.Core.Launch.LaunchOptions` in every component, not just the ones inside it. That is why
the area is `Launch/`. `Components/Proton/` and `Components/Presets/` have the milder version of the
same clash — each shares its last segment with a `Titanite.Core` namespace, so keep
`Titanite.Core.Proton` and `Titanite.Core.Presets` out of `_Imports.razor`: importing either
would make a bare `Proton` or `Presets` ambiguous. Markup needs no types from them — write `var` in
a loop, and expose anything else as a helper on the code-behind, which has its own `using`
directives and no such clash.

Put a component in the area whose job it serves. `GameArtwork` sits in `Game/` rather than
`Library/` because the config dialog uses it too — anything shared by two areas belongs to the
concept it describes, not to whichever area happened to need it first.

**Put logic in a `<Component>.razor.cs` code-behind, not an `@code` block.** The `.razor` file
stays markup. The code-behind is `public partial class <Component> : ComponentBase` in a
namespace matching the folder path, e.g. `Titanite.UI.Components.Library`. Moving a component
between areas means updating that namespace and `_Imports.razor` together, or the markup stops
resolving it.

Consequences worth remembering:

- Inject with an `[Inject]` property in the code-behind, not `@inject` in markup.
- `_Imports.razor` only covers `.razor` files. Code-behind files need their own `using`
  directives — `Microsoft.AspNetCore.Components` at minimum.
- Helpers used by a single component are `private static` on its code-behind. Once a second
  component needs one, move it to `Titanite.UI.Services`' `Formatting/` (`PathDisplay`,
  `FileSizeDisplay`, `LastPlayedDisplay`) rather than duplicating it.

Small markup expectations: `@key` on items rendered in a loop, `title` on text that can be
truncated by `text-overflow`, and `aria-hidden` on purely decorative elements.

**Every `<button>` is a `<Button>`.** `Components/Controls/Button.razor` owns the styling and
the states — hover, disabled, primary, dangerous — so `Variant` and `Size` are how a caller asks
for a look, never a `class`. Six panels used to copy the same `.button` rules into their own
stylesheets and had drifted apart; a new stylesheet must not add `.button` back. A disabled button
drops its variant colouring and has no hover rule at all — hence the only hover selector is
`:hover:not(:disabled)` — so every unavailable action looks the same whatever it would have done.

`Destructive` and `Danger` are the two halves of a two-step button. `Destructive` is an action that
removes something: ordinary at rest, red under the pointer, so the warning arrives before the first
click rather than after it. `Danger` is the armed second step, red already, and it reddens further
on hover rather than turning accent-blue like every other button.

**Icons are components, one SVG per file, in `Components/Icons/`.** They take no parameters and
paint in `currentColor` with `width`/`height` on the `<svg>`, so a consumer sizes and colours them
through the button or link that holds them — `.view-button` changing colour on hover is all it
takes. Each carries `aria-hidden="true"` and `focusable="false"`: the icon is never the name of
anything, so a control that has lost its visible text needs `aria-label` (and `title`, since the
tooltip is now the only way to read what it does).

**Clickable cards use an overlay button, not a wrapper.** A `<button>` may only contain phrasing
content, so it cannot wrap a card holding an `<h2>` or a `<dl>`. The cards render a transparent
button stretched over themselves (`position: absolute; inset: 0`) carrying an `.sr-only` label.
That keeps the markup valid, the whole card clickable, and every card reachable by Tab.

## Styling

**Use component-scoped CSS.** Every component gets a sibling `<Component>.razor.css`. Styles
belong to the component that owns the markup.

**`wwwroot/app.css` is for global styles only** — design tokens, the reset, `body` defaults, and
document-wide utilities such as `.sr-only`. Adding a component's rules there is a bug, even if it
works.

**Never hardcode a colour.** `app.css` defines the palette as custom properties on `:root`
(`--surface-0..2`, `--border`, `--text`, `--text-muted`, `--accent`, `--accent-soft`,
`--warning`, `--warning-soft`, `--danger`). Scoped stylesheets consume them via `var(--token)`.
A new colour is added to the token list first, then referenced — that keeps the theme in one
place and dark-mode consistent.

Scoping gotcha: the scope attribute is applied to elements in *that component's own* markup, so
a parent's stylesheet cannot reach into a child component. Use `::deep` on an element the parent
does own when that is genuinely needed, and prefer moving the rule into the child instead.
`MainLayout.razor.css` shows the legitimate case — `NavLink` renders its own anchor, which never
carries the layout's scope attribute, so the tab styles hang off `.nav ::deep`.

`MainLayout`'s `.content` is a full-height flex column that scrolls and draws no gutter of its own:
the page owns its margin, because a page that has to reach the window edge cannot cancel a parent's
padding without knowing the number. Most pages set `margin: 28px` (`margin: 28px auto`
where it is also `max-width: 1180px`); `GameConfigPanel` and `PresetsPanel` set none, which is what
lets the settings editor run edge to edge under its header band.

A short page needs to do nothing else. A page that should instead pin its chrome and scroll only one region claims the height —
`flex: 1; min-height: 0` on itself, `flex: none` on the parts that stay put, and
`flex: 1; min-height: 0; overflow-y: auto` on the region that scrolls. `GameLibrary` does this so
the header, search and view toggle never leave the window. Omitting either `min-height: 0` makes
the region grow to its content instead, and the whole page scrolls again. An overflow container
clips a focused card's `outline-offset`, so the scroll region carries a few pixels of padding.

A component that renders its own `<li>` keeps its item styling in its own stylesheet, leaving the
container to do nothing but lay children out. That is why `GameGridCard` and `GameListCard` own
the list item rather than `GameLibrary` wrapping them in one.

Photino serves these through the static web assets runtime manifest, so scoped CSS is *not*
copied into the output `wwwroot` — only `app.css` and `index.html` appear there, which is
expected. If styles ever vanish wholesale, check that `Titanite.App` still uses
`Microsoft.NET.Sdk.Razor`; that SDK generates both the style bundle and the manifest that serves
it.

## Data

Components never touch the filesystem or parse a launcher's files, and never name a launcher in
code. They depend on a port from `Titanite.Abstractions.Launchers` — `IGameLibrary`,
`IGameLauncher`, `ILaunchOptionsStore`, `ICompatibilityTools`, `IGameArtwork`,
`IGameLauncherAvailabilityWatcher` — or `IPresetService` from
`Titanite.Abstractions.Presets` — and render what it returns. Steam is the only launcher today
and nothing up here says so: a game is a `GameId`, which is a launcher name and an identifier.

The word "Steam" may appear in copy a person reads. It may not appear in a type, a member or a
variable; `ProjectTierTests` fails the build if it does.

**A screen with load-edit-save state gets a presenter**, in `Titanite.UI.Services.Presentation`,
injected like any other service. `GameConfigPanel`, `PresetsPanel` and `GameLibrary` each
have one. The presenter owns everything that survives a re-render and everything that talks to a
port; the component owns markup and the view state a presenter has no business knowing — which
dialog is open, and whether a two-step button is waiting for its second click. A message the
person reads is a `StatusMessage`, which carries its own tone, rather than a string beside a bool.

**What the launcher has stored can change while a screen is open, so that is a subscription too.**
Someone can edit a game in Steam itself, or a preset can be applied to it from elsewhere.
`IGameConfigurationWatcher` reports a game whose stored launch options or Proton build moved;
`GameConfigPanel` follows the game it is showing and drops it when it goes away. The presenter's
`RefreshAsync` decides what that means: with nothing unsaved it takes the new settings on, and with
unsaved edits it keeps them and warns, because throwing away what someone typed is worse than
showing them something stale.

**Whether a launcher can be saved to is a subscription, not a question.** A panel that saves
inherits `LauncherAvailabilityView`, which reads `IGameLauncherAvailabilityWatcher.Current` for its
first render and subscribes for the rest. Render `UnavailableReason` verbatim — the adapter writes
that sentence because only it knows why. The event can arrive on a pool thread, which is why the
base class wraps its handler in `InvokeAsync`.

**A choice that outlives the session belongs to `AppSettings`, which means its type belongs to
`Titanite.Core.Settings`.** `LibraryViewMode` and `LibrarySortOrder` sit there rather than beside
`GameLibrary`, because `Core` cannot reference the UI. The presentation — `LibrarySortOrders.Title()`
and `.Apply()` — sits in `Titanite.UI.Services`. Read the stored value when the screen opens and
write it back when it changes; do not give the property a field initialiser, or a first run
disagrees with a stored file that has no value for it yet.

**Which settings exist is data, not code.** `SettingCatalog` is injected, read once at startup
from the YAML files in `Titanite.Catalog`. Never hardcode a variable name or a section
in a component: adding either is an edit to those files. That goes for the headings inside a
section too — a section's file declares its own groups, and `SettingCatalog.GroupsIn` returns them
in the order it wrote them. `CollapsibleGroup` draws one, and everything with headings inside a
configuration tab uses it: the settings list, a command's flag groups, and a compound variable's
option groups. It is a `details` element, so the open state belongs to the browser rather than to
a field — the render tree always says `open`, which is what keeps a re-render from reopening a
group somebody closed. Key it wherever the list it sits in can change, or a closed group comes
back as a different one. The exceptions are the two section ids in `SettingCategoryIds`, which
carry a control that is not a list of variables — the CPU affinity picker and MangoHud's
launch-chain toggle.

Because the sections come from files, there is no section to select until they are read. A
component that opens on one has to pick it after the catalogue is available rather than in a field
initialiser, or it renders with nothing selected.

Every load path handles four states explicitly: loading, error, empty, and populated. Steam may
be missing, mid-write, or installed with no games — a component that only renders the happy path
is incomplete. Catch at the component boundary and surface a message rather than letting an
exception reach the Photino window.

Cover art is the one network dependency, fetched from Steam's CDN by the `<img>` itself. Treat it
as optional in every sense: Steam publishes no artwork for several compatibility tools, and the
user may be offline. `GameArtwork` falls back to a lettered tile on load failure, so a missing
image is a normal outcome rather than an error. Nothing else here should assume internet access.
