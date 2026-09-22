# Setting definitions

One file per section of the configuration screen. Adding a section means adding a file; adding a
variable means adding an entry to one. Neither needs a change to the application.

```yaml
id: dlss                # Stable key. Referred to by name in code, so do not rename lightly.
title: DLSS             # The heading shown to a person.
order: 1                # Where the section sits in the list, lowest first.

settings:
  - variable: PROTON_DLSS_UPGRADE   # The environment variable, exactly as it is written.
    label: Upgrade DLSS libraries   # A readable name.
    description: What it actually does, in one sentence.
    kind: toggle                    # toggle | choice | text | number
    on: "1"                         # toggle only: the value written when it is switched on.
    choices: [a, b]                 # choice only: the values offered.
    placeholder: "144"              # text and number only: an example value.
    protonBuilds: ["^GE-Proton"]    # optional: builds this applies to, as regular expressions.
    restrictToProtonBuild: true     # optional: hide it elsewhere, rather than greying it out.
    hideUnlessSet: true             # optional: list it only where a game already has it set.
    allowEmpty: true                # text only: an empty value is written, not removed.
```

Only `variable` and `label` are required. `kind` defaults to `text`, and `on` to `1`.

A `description` is one short sentence — the screen shows it only when *Show variable descriptions*
is switched on in Settings, and shows the label and the variable name whichever way that sits. It
is read by the search either way, so a word worth finding a setting by belongs in it even while it
is off screen. Note that a plain YAML scalar cannot contain `: `, so write an aside with a dash.

## Headings within a section

A section of twenty variables is a section nobody reads. Give it `groups` and each one is listed
under a heading, in the order the file declares them.

```yaml
settings:                       # optional: listed first, under no heading at all.
  - variable: DXVK_HDR
    label: Enable HDR in DXVK

groups:
  - name: DLSS                  # the heading, shown above the settings beneath it.
    settings:
      - variable: PROTON_DLSS_UPGRADE
        label: Upgrade DLSS libraries
```

A file may use either form or both. `settings` is what a section short enough to need no headings
writes on its own, and where both appear the ungrouped ones come first — so a section can gain
headings without its opening few settings moving.

Every heading collapses, and every one starts open. A closed group shows the number of settings
configured inside it beside its name, so closing one never hides that something is set. The same
goes for a compound variable's option groups and a command's flag groups — Gamescope's and
MangoHud's headings behave exactly like these.

Headings are presentation and nothing else. A setting belongs to the section its file names, and
grouping never changes that, so moving one between headings does not change where it is found or
how it is written. A group is a run rather than a lookup: naming the same heading twice leaves two
runs where they were written rather than merging them somewhere up the list. A heading whose every
setting is hidden on the build in force is hidden with them rather than standing over nothing.

## Variables that hold several settings at once

Some variables are really lists — `MANGOHUD_CONFIG`, `DXVK_HUD`. Give one a `compound` block and
it is edited option by option instead of as a line of text, whatever its `kind` says.

```yaml
  - variable: MANGOHUD_CONFIG
    label: MangoHud options
    compound:
      separator: ","          # between entries. Defaults to a comma.
      assignment: "="         # between a key and its value. Defaults to an equals sign.
      groups:
        - name: Frame limiting    # optional: a group with no name shows no heading.
          options:
            - key: fps_limit
              label: Frame rate limit
              kind: text          # toggle | choice | text | number
              placeholder: "224"
              description: One limit, or several separated by commas to cycle between them.

            - key: fps            # no kind, so a flag: written as the bare key, with no value.
              label: Frame rate
```

Two differences from an ordinary setting are worth knowing. An option's `kind` defaults to
`toggle`, not `text`, because these formats are mostly flags. And a toggle here writes the bare
key rather than a value, which is what a flag means.

These lists are always partial — MangoHud alone has well over a hundred options. Whatever is not
listed stays editable in the free-text field beneath the controls and is carried through
untouched, so nothing is out of reach and nothing is lost by Titanite not knowing about it.

## Sections that configure a command

Not everything worth setting is an environment variable. Gamescope is configured entirely by flags
on the command that launches it, so a section may declare a `command` block alongside — or instead
of — its `settings`.

```yaml
command:
  name: gamescope           # The command written into the launch chain.
  label: Launch through Gamescope
  description: What running the game through it does.
  terminator: "--"          # optional: what ends the command's own arguments.
  groups:
    - name: Output          # optional: a group with no name shows no heading.
      flags:
        - flag: "-W"                    # As it is written, leading dashes and all.
          aliases: ["--output-width"]   # optional: other spellings, recognised when read.
          label: Output width
          kind: number                  # toggle | choice | text | number
          placeholder: "3840"
          description: One sentence on what it does.

        - flag: "-f"        # no kind, so a switch: written bare, with no value.
          label: Fullscreen
```

Only `name` and `flag` are required. As with a compound variable's options, a flag's `kind`
defaults to `toggle` rather than `text`, because command lines are mostly switches.

`terminator` is what tells Titanite where the command's arguments stop and the next command in
the chain begins. Declare it for any command that takes one; a command without it — `mangohud`,
`gamemoderun` — is treated as taking no arguments of its own rather than claiming what follows it.

Setting any flag adds the command to the chain if it is not already there, and switching the
command off takes its flags with it. Flags Titanite does not list survive both untouched, and
`aliases` is what stops a flag someone wrote out in full from being duplicated rather than edited.

## `protonBuilds`

A list of regular expressions matched against a build's name and its version string. If any
matches, the setting applies; if the list is absent, the setting is offered for every build.

The patterns the shipped files use are:

| Pattern | Matches |
| --- | --- |
| `^GE-Proton` | `GE-Proton11-6-x86_64` by name, `GE-Proton11-6` by version |
| `^(proton-)?cachyos` | `proton-cachyos-…-x86_64_v3` by name, `cachyos-11.0-…` by version |

Both spellings are needed for CachyOS because the `version` file drops the leading `proton-`, and
a build unpacked under a different directory name is then still recognised. Valve's builds —
`proton_experimental`, `proton_hotfix`, `proton_9` — are deliberately named by no pattern: they are
the family that reads none of the settings these lists guard.

Naming builds is only worth doing where the answer cannot be read. For a `PROTON_` variable the
build's own launch script settles it exactly, so a list adds nothing unless
`restrictToProtonBuild` is also set — greying out is already handled, and hiding is the only thing
left for a declaration to ask for.

Use it for a setting that only ever exists in one family of builds. It is a declaration, not a
guess — separately from this, Titanite reads each installed build's own launch script and dims
anything that build does not consult. The two agree in the usual case, and where a variable is
implemented somewhere Titanite cannot read, this list is the only thing that can speak for it.

By default a setting that does not apply is still shown, greyed out, saying why it does nothing.
Add `restrictToProtonBuild: true` to hide it instead. Use that where the setting exists in one
family of builds and nowhere else: a list of GE-Proton features shown against Valve's Proton is
not a set of choices to reconsider, it is noise in a list someone is trying to read. A setting
that already has a value stays visible whatever this says, or it could neither be seen nor
removed.

## Two ids the application knows by name

`cpu` and `mangohud` each carry a control that is more than a text box — the affinity picker, and
the toggle that puts `mangohud` in the launch chain. Renaming those ids removes the control rather
than the section, so rename them only alongside the code that looks for them.

Nothing else is known by name. Nvidia's DLSS settings used to be, held back by the editor so they
could be listed under their own heading; that heading is now declared in the file like any other.

## `hideUnlessSet`

Some switches have two names. `PROTON_ENABLE_WAYLAND` and `PROTON_USE_WAYLAND` are one setting,
and every build that reads either reads both; the same goes for `PROTON_ENABLE_HDR` and
`PROTON_USE_HDR` in GE-Proton. Listing both offers the same switch twice, and listing only the
current one drops a game already using the other into custom variables, where it is unlabelled.

`hideUnlessSet: true` is the third answer. The setting is left out of its section entirely until a
game has it written, and appears in place with its own label as soon as one does. Declare it on the
older name and leave the current one listed normally, so a new configuration is offered one switch
and an existing one is still shown what it says.

It is stronger than `restrictToProtonBuild`, which hides a setting only where it does not apply.
A setting carrying both is hidden everywhere but where it is set, which is what a second spelling
wants: `protonBuilds` still describes where the variable works, so a game that has it set is shown
it plainly on a build that reads it rather than greyed out.

## `allowEmpty`

Clearing a text box normally removes its variable, because for almost every variable an empty
value means nothing and a stray `DXVK_CONFIG=` is clutter. A few variables mean something empty.
`LD_PRELOAD=` is the usual way to keep Steam's overlay library out of a game, and removing the
variable instead leaves the overlay where it was.

`allowEmpty: true` makes a text setting behave like a custom variable. Clearing the box writes the
variable empty, a *Set empty* button does the same where nothing is set, and a *Remove* button is
what takes it away. Only text settings read it; a toggle, choice or number ignores it.

## Variables Titanite does not know

Anything missing from these files still parses and is still written back. It appears under custom
variables rather than in a named section, so an unknown variable costs presentation and never
data.
