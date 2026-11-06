---
name: Fluent Glass Ergonomics
colors:
  surface: '#131315'
  surface-dim: '#131315'
  surface-bright: '#39393b'
  surface-container-lowest: '#0e0e10'
  surface-container-low: '#1b1b1d'
  surface-container: '#1f1f21'
  surface-container-high: '#2a2a2c'
  surface-container-highest: '#353437'
  on-surface: '#e4e2e4'
  on-surface-variant: '#bdc8cf'
  inverse-surface: '#e4e2e4'
  inverse-on-surface: '#303032'
  outline: '#889299'
  outline-variant: '#3e484e'
  surface-tint: '#74d1ff'
  primary: '#b4e4ff'
  on-primary: '#003548'
  primary-container: '#60cdff'
  on-primary-container: '#005572'
  inverse-primary: '#006687'
  secondary: '#a3c9ff'
  on-secondary: '#00315c'
  secondary-container: '#3b93f1'
  on-secondary-container: '#002a51'
  tertiary: '#ffd6a4'
  on-tertiary: '#452b00'
  tertiary-container: '#fcb347'
  on-tertiary-container: '#6e4700'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#c1e8ff'
  primary-fixed-dim: '#74d1ff'
  on-primary-fixed: '#001e2b'
  on-primary-fixed-variant: '#004d67'
  secondary-fixed: '#d3e3ff'
  secondary-fixed-dim: '#a3c9ff'
  on-secondary-fixed: '#001c39'
  on-secondary-fixed-variant: '#004883'
  tertiary-fixed: '#ffddb4'
  tertiary-fixed-dim: '#ffb955'
  on-tertiary-fixed: '#291800'
  on-tertiary-fixed-variant: '#633f00'
  background: '#131315'
  on-background: '#e4e2e4'
  surface-variant: '#353437'
typography:
  display:
    fontFamily: Geist
    fontSize: 40px
    fontWeight: '600'
    lineHeight: 52px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Geist
    fontSize: 28px
    fontWeight: '600'
    lineHeight: 36px
    letterSpacing: -0.015em
  headline-md:
    fontFamily: Geist
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 26px
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: Geist
    fontSize: 16px
    fontWeight: '600'
    lineHeight: 22px
    letterSpacing: -0.005em
  body-lg:
    fontFamily: Geist
    fontSize: 15px
    fontWeight: '400'
    lineHeight: 22px
    letterSpacing: 0em
  body-md:
    fontFamily: Geist
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
    letterSpacing: 0em
  body-sm:
    fontFamily: Geist
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
    letterSpacing: 0.005em
  label-md:
    fontFamily: Geist
    fontSize: 13px
    fontWeight: '500'
    lineHeight: 18px
    letterSpacing: 0.01em
  label-sm:
    fontFamily: Geist
    fontSize: 11px
    fontWeight: '500'
    lineHeight: 14px
    letterSpacing: 0.02em
  caption:
    fontFamily: Geist
    fontSize: 10px
    fontWeight: '400'
    lineHeight: 14px
    letterSpacing: 0.025em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  gutter: 1rem
  gutter-compact: 0.5rem
  margin: 1.5rem
  margin-window: 0.75rem
  space-xs: 0.25rem
  space-sm: 0.5rem
  space-md: 0.75rem
  space-lg: 1.25rem
  space-xl: 2rem
---

## Brand & Style
This design system captures the precision of modern desktop ergonomics, fusing Windows 11 Fluent Design principles with the refraction and optical clarity of multi-layered liquid glass. The atmosphere is quiet, deliberate, and technologically pristine. It targets demanding desktop knowledge workers, creators, and systems operators who value deep ambient focus, low cognitive friction, and structural predictability.

Visually, the style synthesizes:
- **Mica & Mica Alt Tiering:** Dynamic desktop base layers that sample background tinting without distracting saturation.
- **Acrylic Translucency:** Depth-mapped, low-pass backdrop blurs applied selectively to temporary surfaces, flyouts, and contextual overlays.
- **Optical Specular Highlighting:** Fine, one-pixel top-edge luminance that mimics light falling naturally across physical polycarbonate and crystal planes.
- **Controlled Restraint:** Clean geometric order, subtle micro-interactions, strictly sans-serif typographic hierarchies, and zero kitsch or terminal clichés.

## Colors
The color architecture relies on deep, slate-charcoal neutrals layered to create visual altitude without stark pitch-black drops, paired with the iconic electric Windows cyan-blue spectrum.

### Functional Roles & Behavior
- **Accent Core (`#60CDFF`):** Used on dark modes for actionable interactive surfaces, focused indicators, selection states, and priority badges. High legibility against dark slate.
- **Accent Anchor (`#0078D4`):** Used for pressed/active kinetic states, primary control gradients, and high-emphasis desktop fills.
- **Mica Base Canvas (`#202020` / `#181818`):** The structural bedrock. Holds a 4% neutral saturation tint to preserve window depth against display wallpapers.
- **Layer Surfaces (`#2C2C2E` at 70% to 85% opacity):** Elevation tiers for content grouping, structured cards, side navigation panes, and modal dialogs.
- **Specular Border Gradients:** Linear strokes moving from `rgba(255, 255, 255, 0.12)` at the top apex down to `rgba(255, 255, 255, 0.03)` at the bottom edge.

## Typography
Typographic clarity is strictly maintained using geometric, low-contrast grotesque anatomy with systematic tabular numerals and balanced counters (instantiated via Geist to evoke Segoe UI Variable Display and Text optical masters).

- **Optical Scaling:** Display and Headline roles use compact line-heights and slight negative tracking (`-0.01em` to `-0.02em`) to bind multi-word titles closely together.
- **Desktop Ergonomics:** Body copy is grounded at a crisp 14px with a 20px line-height, preserving spatial efficiency in dense layouts while preventing visual strain.
- **Strict Absence of Serifs:** Serif forms, decorative callouts, and typewriter glyphs are entirely banned from the UI runtime.

## Layout & Spacing
The layout follows a fluid window model partitioned by persistent structural frames (Navigation Rail/Pane, Master Detail Splitters, and Canvas Workspace) operating on a strict 4px/8px modular cadence.

### Grid & Density
- **Application Shell:** Uses fluid containers with fixed-width sidebars (collapsed: 48px, expanded: 240px) and auto-stretching primary work surfaces.
- **Section Gutters:** Standard 16px (`1rem`) gap on high-resolution displays; tightens to 8px (`0.5rem`) within toolbars, inspector panes, and command bars.
- **Responsive Handling:**
  - *Large Desktops (>1440px):* Multi-column master-detail-inspector displays active simultaneously.
  - *Laptops/Tablets (768px - 1439px):* Inspector panes convert to sliding flyout drawers; side rails collapse into icon-only modes.
  - *Compact Windows (<768px):* Single-view stack navigation with bottom-anchored or top command bar switches.

## Elevation & Depth
Depth in this system is driven by material physics and translucency rather than heavy drop shadows:

1. **Base Tier (Mica Alt):** The lowest layer of window frames. Subtle surface tinting (`#1C1C1E` at 92% opacity) through which underlying system desktop colors minimally bleed using `backdrop-filter: blur(40px) saturate(125%)`.
2. **Intermediate Tier (Solid/Translucent Cards):** Elevated workspaces resting on the Mica base use `#2C2C2E` at 70% opacity, framed by a continuous 1px outline featuring a vertical gradient: `rgba(255, 255, 255, 0.09)` descending to `rgba(255, 255, 255, 0.02)`.
3. **Transient Tier (Acrylic Surfaces):** Context menus, popovers, select dropdowns, and flyout overlays use an aggressive blur (`backdrop-filter: blur(24px) saturate(160%)`) with background tint `#252528` at 65% opacity.
4. **Specular Edge Highlighting:** Focused or elevated components exhibit an internal rim light (a top inset shadow of `inset 0 1px 0 rgba(255, 255, 255, 0.16)`), reproducing physical liquid glass refraction.
5. **Ambient Shadow Casts:** Reserved strictly for detached transient overlays: `0 8px 32px rgba(0, 0, 0, 0.36), 0 2px 6px rgba(0, 0, 0, 0.24)`.

## Shapes
Shapes express the standard Windows 11 geometry: restrained, subtle, and non-distracting.
- **Standard Controls (Inputs, Buttons, Lists, Grid Items):** `0.25rem` (4px to 6px) radius, creating clean geometric stability without harsh corners.
- **Containers & Surfaces (Cards, Dialogs, Flyouts):** `0.5rem` (8px) radius with strictly masked border radiuses ensuring inner specular outlines match external corner curvatures.
- **Pills/Indicators:** Rounded full (pill) is reserved strictly for status pills, notification pips, and toggle switch handles.

## Components

### Buttons
- **Standard Button:** Background `rgba(255, 255, 255, 0.06)`, specular top border `1px solid rgba(255, 255, 255, 0.12)`, text `#FFFFFF`. Hover elevates opacity to `0.09` with an instantaneous 100ms ease-out. Active/Pressed applies a micro-scale of `scale(0.985)` with background `rgba(255, 255, 255, 0.03)`.
- **Accent Primary Button:** Solid gradient fill from `#60CDFF` to `#0078D4`, text `#000000` (bold weight for sharp contrast) or pure white depending on luminance threshold. Top border highlight `rgba(255, 255, 255, 0.35)`.
- **Subtle / Ghost Button:** Transparent background, zero border by default, `rgba(255, 255, 255, 0.06)` fill on cursor hover.

### Inputs & Text Fields
- Rectangular surface using `#1F1F21` at 80% opacity with a recessed inset shadow (`inset 0 1px 2px rgba(0,0,0,0.4)`).
- Bottom border acts as the active indicator: 1px subtle gray when idling; activates an animated 2px `#60CDFF` horizontal accent bar originating from center upon focus.

### Cards & Grouping Surfaces
- Translucent layered plates. Padded with `1.25rem` (`space-lg`), bounded by a fine `0.5rem` radius and the system specular top highlight.
- No heavy dark drops; depth separation occurs exclusively through color value shift and blur layering.

### Selection Controls (Checkbox & Radio)
- **Checkbox:** 18x18px box, `4px` radius. Resting state uses a 1px border `rgba(255, 255, 255, 0.45)`. Checked state transitions instantly to `#60CDFF` background with `#000000` check icon glyph.
- **Toggle Switch:** Pill-shaped track (40x20px) filled with `#323236`. Checked track shifts to `#60CDFF`. The handle is a smooth 14px circular disc with an ambient physical shadow.

### Menus & Acrylic Flyouts
- Floating surfaces utilizing Acrylic transparency (`backdrop-filter: blur(24px)`, `#252528` at 70%).
- Menu items have a 3px left indicator highlight on hover, rounded 4px selection bounding boxes, and right-aligned monospaced keyboard shortcuts in `rgba(255, 255, 255, 0.5)`.