---
name: bootstrap-material-ui
description: Apply Bootstrap components and utilities using Material Design visual principles — flat, minimalistic, enterprise-grade dark theme. Use this skill any time UI components, layouts, or CSS are being added or modified.
---

# Bootstrap + Material Design UI Skill

This project uses **Bootstrap for structure** (grid, modal shell, flex utilities) and **Material Design for visual language**. The result is a dark-themed, flat, enterprise-grade UI.

## Core Philosophy

- **Never use Bootstrap's default light theme aesthetics** — no white backgrounds, no heavy border-radius on form elements, no default box shadows on cards
- **Flat surfaces**: elevation is communicated through opacity overlays on a dark background, not shadows
- **Minimalistic**: whitespace and typography weight create hierarchy; avoid decorative borders, gradients on surfaces, or bright accent overuse
- **Enterprise-grade**: consistent spacing, predictable interactive states, legible typography

## Color Tokens

Always use these values. Do not introduce new colors without good reason.

| Role | Value |
|------|-------|
| Page background | `#0d0d1a` |
| Surface (modal, card) | `#1a1a2e` |
| Inner card (frosted) | `rgba(255,255,255,0.05)` |
| Text primary | `#e8eaf6` |
| Text secondary | `rgba(255,255,255,0.5)` |
| Text muted | `rgba(255,255,255,0.35)` |
| Primary accent | `#5c6bc0` |
| Primary accent light | `#7986cb` |
| Warning / gold | `#f59e0b` / `#fcd34d` |
| Danger / red | `#ef5350` |
| Border subtle | `rgba(255,255,255,0.08)` |
| Border visible | `rgba(255,255,255,0.12)` |

## Elevation (Dark Theme)

Use background opacity overlays — **not box-shadow** — to distinguish layers:

```css
/* barely lifted — app bar, headers */
background: rgba(255, 255, 255, 0.03);

/* card / inner panel */
background: rgba(255, 255, 255, 0.05);

/* hover / active chip */
background: rgba(255, 255, 255, 0.08);
```

## Component Recipes

### App Bar
```css
.app-bar {
    height: 56px;
    padding: 0 8px;
    background: rgba(255, 255, 255, 0.03);
    border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    display: flex;
    align-items: center;
    gap: 4px;
}
```

### Icon Button (Material FAB-lite)
Always use `.icon-btn` for toolbar/icon actions. 40×40px circular target:

```css
.icon-btn {
    width: 40px;
    height: 40px;
    border-radius: 50%;
    border: none;
    background: transparent;
    color: rgba(255, 255, 255, 0.6);
    cursor: pointer;
    transition: background 0.15s, color 0.15s;
    display: flex;
    align-items: center;
    justify-content: center;
}
.icon-btn:hover { background: rgba(255, 255, 255, 0.1); color: #fff; }
.icon-btn:active { background: rgba(255, 255, 255, 0.16); }

/* Active/selected state */
.icon-btn--active {
    color: #7986cb !important;
    background: rgba(121, 134, 203, 0.15) !important;
}
```

### Chip (Player name, tags, badges)
```css
.chip {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 4px 14px;
    border-radius: 20px;
    background: rgba(255, 255, 255, 0.06);
    border: 1px solid rgba(255, 255, 255, 0.1);
    color: rgba(255, 255, 255, 0.55);
    font-size: 0.78rem;
    cursor: pointer;
    transition: background 0.15s, color 0.15s;
}
.chip:hover { background: rgba(255, 255, 255, 0.11); color: rgba(255, 255, 255, 0.9); }

/* Active/named variant */
.chip--active {
    color: #7986cb;
    border-color: rgba(92, 107, 192, 0.35);
    background: rgba(92, 107, 192, 0.1);
}
```

### Tab Bar (Material tab indicator)
```css
.tab-bar {
    display: flex;
    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
    gap: 0;
}
.tab {
    background: none;
    border: none;
    border-bottom: 2px solid transparent;
    color: rgba(255, 255, 255, 0.4);
    font-size: 0.75rem;
    font-weight: 600;
    letter-spacing: 0.1em;
    text-transform: uppercase;
    padding: 0.6rem 1.25rem;
    cursor: pointer;
    margin-bottom: -1px;
    transition: color 0.15s;
}
.tab:hover { color: rgba(255, 255, 255, 0.7); }
.tab--active {
    color: #7986cb;
    border-bottom-color: #7986cb;
}
```

### Modal Body (no modal-header / modal-footer)
```html
<BSModal @ref="Modal" Id="myModal" IsVerticallyCentered="true" Size="ModalSize.Large">
    <Body>
        <div class="my-container">
            <!-- Header integrated into body -->
            <div class="my-header">
                <h5 class="my-title">Title</h5>
            </div>

            <!-- Content -->

            <!-- Footer integrated into body -->
            <div class="my-footer">
                <button type="button" class="my-btn" @onclick="() => Modal?.Hide()">Close</button>
            </div>
        </div>
    </Body>
</BSModal>
```
```css
.my-container {
    color: #e8eaf6;
    padding: 1.5rem;
    max-height: 80vh;
    overflow-y: auto;
}
.my-header {
    margin-bottom: 1.5rem;
    padding-bottom: 1rem;
    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}
.my-title {
    font-size: 1.4rem;
    font-weight: 300;
    color: #fff;
    margin: 0;
}
.my-footer {
    margin-top: 1.5rem;
    padding-top: 1rem;
    border-top: 1px solid rgba(255, 255, 255, 0.07);
    text-align: center;
}
```

### Action Button
```css
.action-btn {
    background: #5c6bc0;
    border: none;
    color: #fff;
    font-size: 0.78rem;
    font-weight: 700;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    padding: 0.6rem 1.75rem;
    border-radius: 4px;
    cursor: pointer;
    transition: background 0.15s;
}
.action-btn:hover { background: #7986cb; }
```

## Typography Scale

| Usage | Size | Weight | Notes |
|-------|------|--------|-------|
| Display | `clamp(4.5rem, 20vw, 10rem)` | 900 | Gradient text fill |
| Section title | `1.4–1.5rem` | 300–700 | Light = secondary, bold = primary |
| Body | `0.82–0.92rem` | 400–500 | `color: rgba(255,255,255,0.85)` |
| Label / overline | `0.68–0.75rem` | 700 | Uppercase, `letter-spacing: 0.1–0.14em`, muted color |
| Caption | `0.68rem` | 400 | `color: rgba(255,255,255,0.35)` |

## Motion

```css
/* Hover / focus — always fast */
transition: background 0.15s, color 0.15s;

/* Entrance animations */
animation: slide-in 0.35s ease-out both;

/* Spring / pop-in */
animation: pop-in 0.25s cubic-bezier(0.175, 0.885, 0.32, 1.275);

/* Breathing / pulse */
animation: fade-pulse 2.5s ease-in-out infinite;
```

## CSS Naming Convention

- Component-scoped styles use a **2–3 letter kebab prefix** in `.razor.css`:
  - `hp-` → HelpModal, `lb-` → LeaderboardModal, `sm-` → SaveScoreModal, `pn-` → PlayerNameModal
- Global utilities in `Client/wwwroot/css/app.css` only
- **Never add `background` or gradient rules to individual modal `.razor.css`** — the global `.modal-content` in `app.css` handles that

## What NOT To Do

| Don't | Do instead |
|-------|-----------|
| `box-shadow: 0 4px 20px rgba(0,0,0,0.5)` on surfaces | Use `rgba(255,255,255,0.05)` background overlay |
| `background: linear-gradient(...)` on modal/card surfaces | Keep surfaces flat — `#1a1a2e` |
| Bootstrap's `.card`, `.card-body` with default styles | Custom container with dark theme CSS |
| `border-radius: 50px` pill buttons everywhere | `border-radius: 4px` for action buttons; `20px` for chips only |
| `!important` overrides everywhere | Use component-scoped `.razor.css` specificity |
| Heavy decorative borders | Single `1px solid rgba(255,255,255,0.1)` dividers only |
| Bootstrap's default `.btn-primary` blue | Custom accent `#5c6bc0` / `#7986cb` |
