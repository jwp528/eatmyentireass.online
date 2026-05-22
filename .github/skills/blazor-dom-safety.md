---
name: blazor-dom-safety
description: Rules for writing crash-safe Blazor components that handle async events or receive parent-cascade re-renders. Use this skill when writing or reviewing any Blazor component with async event handlers, modal close logic, or conditional rendering.
---

# Blazor DOM Safety Skill

## The Problem

Blazor WASM can crash with `TypeError: can't access property "removeChild", r.parentNode is null` when:

1. An async event handler fires (e.g., button click calling `async Task Save()`)
2. A parent component cascades a re-render down (e.g., after `EventCallback.InvokeAsync`)

These events can trigger **2–3 concurrent render cycles** on the same component nearly simultaneously.

### Root Cause: IHandleEvent.HandleEventAsync

When an `async Task` event handler runs on a Blazor component:
1. **Render #1** fires at the **first `await`** (before the awaited work completes)
2. **Render #2** fires on **async completion**
3. **Render #3** fires if a parent `EventCallback.InvokeAsync` triggers a cascade

If an `@if` block **changes state** between any two of these renders, the second render tries to call `removeChild` on DOM nodes the first already removed → **crash**.

---

## The Rule

> **Never use `@if` to add or remove DOM elements inside components that handle async events or receive cascaded re-renders.**

---

## The Fix: CSS Display Toggle

Always render both elements. Toggle visibility via `style="display:none"` or a CSS class. Blazor only updates attributes — it **never calls `removeChild`**.

### Pattern

```razor
@* ❌ BAD — crashes under concurrent renders *@
@if (isNamed)
{
    <span class="name">@playerName</span>
}
else
{
    <i class="fas fa-user-plus"></i>
}

@* ✅ GOOD — always rendered, Blazor only updates an attribute *@
<span class="name" style="@(!isNamed ? "display:none" : "")">@playerName</span>
<i class="fas fa-user-plus" style="@(isNamed ? "display:none" : "")"></i>
```

### Single Element with Ternary (when element type doesn't change)

If only the CSS class or text content changes (not the element type), a ternary is safe and preferred:

```razor
@* ✅ SAFE — single element, only attributes change *@
<button class="icon-btn @(isActive ? "icon-btn--active" : "")">
    <i class="fas @(isNamed ? "fa-user" : "fa-user-plus")"></i>
    <span>@(isNamed ? playerName : "Set Name")</span>
</button>
```

This is safe because Blazor diffs attributes/text — it never removes the `<button>` from the DOM.

---

## Modal Close: ShouldRender() Guard

When a modal closes and calls a parent callback, the parent re-renders and cascades back down. Without a guard, the modal renders again with `_modalVisible = false` while its template has already been torn down.

### Pattern

```csharp
// In the modal's code-behind (.razor.cs)
protected override bool ShouldRender() => _modalVisible;
```

```csharp
private async Task Save()
{
    // ...validation logic...

    await SettingsService.SetLastPlayerNameAsync(_name.Trim());

    // Set _modalVisible = false BEFORE the cascade-triggering await
    // This ensures ShouldRender() returns false for ALL subsequent renders
    _modalVisible = false;
    Modal?.Hide();

    // This triggers a parent re-render which cascades back down.
    // ShouldRender() = false blocks it — no crash.
    if (OnNameSaved.HasDelegate)
        await OnNameSaved.InvokeAsync(_name.Trim());
}
```

**Critical ordering**: `_modalVisible = false` must be set **before** `OnNameSaved.InvokeAsync`. If set after, the cascade arrives before the guard is up.

---

## BSModal Reference Implementation

`Client/Components/BSModal.razor` uses the CSS display toggle pattern correctly. It is the reference implementation for this project:

```razor
@* Always rendered — visibility via display: and CSS class only *@
<div class="modal-backdrop fade @(_isVisible ? "show" : "")"
     style="display:@(_isVisible ? "block" : "none")"></div>
<div class="modal @(_isVisible ? "show" : "")"
     style="display:@(_isVisible ? "block" : "none");">
    <div class="modal-content">
        <div class="modal-body">@Body</div>
    </div>
</div>
```

**Do not regress this back to `@if (_isVisible)`**.

---

## Quick Reference

| Scenario | Safe approach |
|----------|--------------|
| Toggle between two different elements | `display:none` toggle on both |
| Toggle CSS class on same element | Ternary in `class=""` attribute |
| Toggle text content | Ternary inline `@(condition ? "A" : "B")` |
| Modal close with parent callback | `ShouldRender() => _modalVisible` + set false before cascade |
| Conditional list items | OK — Blazor's diffing handles list changes safely when using `@key` |
| `@foreach` over changing collections | Add `@key` to uniquely identify each item |
