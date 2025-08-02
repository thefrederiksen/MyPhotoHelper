# MyPhotoHelper Component Library

This document provides practical examples of how to implement the style guide in your Blazor components.

## Quick Start

### 1. Import Variables
Make sure your `site.css` imports the variables:
```css
@import url('variables.css');
```

### 2. Use CSS Variables
Instead of hardcoded values, use the design system variables:
```css
.my-component {
    background-color: var(--primary-blue);
    padding: var(--spacing-md);
    border-radius: var(--border-radius);
    box-shadow: var(--shadow);
}
```

## Component Examples

### Button Components

#### Primary Button
```razor
<button class="btn btn-primary">
    <span class="oi oi-plus" aria-hidden="true"></span>
    Add Photo
</button>
```

#### Secondary Button
```razor
<button class="btn btn-secondary">
    <span class="oi oi-cog" aria-hidden="true"></span>
    Settings
</button>
```

#### Custom Button with Variables
```css
/* CustomButton.razor.css */
.custom-button {
    background-color: var(--primary-blue);
    color: white;
    border: none;
    padding: var(--spacing-sm) var(--spacing-md);
    border-radius: var(--border-radius);
    font-weight: var(--font-weight-medium);
    transition: var(--transition-normal);
    cursor: pointer;
}

.custom-button:hover {
    background-color: var(--primary-blue-dark);
    transform: translateY(-1px);
    box-shadow: var(--shadow-md);
}

.custom-button:focus {
    outline: 2px solid var(--primary-blue-light);
    outline-offset: 2px;
}
```

### Card Components

#### Photo Card
```razor
<div class="photo-card">
    <img src="@photo.ThumbnailUrl" alt="@photo.Description" class="photo-card__image" />
    <div class="photo-card__content">
        <h3 class="photo-card__title">@photo.Title</h3>
        <p class="photo-card__date">@photo.DateTaken.ToString("MMM dd, yyyy")</p>
        <div class="photo-card__actions">
            <button class="btn btn-sm btn-primary">View</button>
            <button class="btn btn-sm btn-secondary">Edit</button>
        </div>
    </div>
</div>
```

```css
/* PhotoCard.razor.css */
.photo-card {
    background: white;
    border: var(--border-width) solid var(--gray-200);
    border-radius: var(--border-radius-md);
    padding: var(--spacing-md);
    box-shadow: var(--shadow);
    transition: var(--transition-normal);
    overflow: hidden;
}

.photo-card:hover {
    box-shadow: var(--shadow-md);
    transform: translateY(-2px);
}

.photo-card__image {
    width: 100%;
    height: 200px;
    object-fit: cover;
    border-radius: var(--border-radius-sm);
    margin-bottom: var(--spacing-sm);
}

.photo-card__content {
    padding: var(--spacing-sm) 0;
}

.photo-card__title {
    font-size: var(--font-size-lg);
    font-weight: var(--font-weight-semibold);
    color: var(--gray-900);
    margin-bottom: var(--spacing-xs);
}

.photo-card__date {
    font-size: var(--font-size-sm);
    color: var(--gray-600);
    margin-bottom: var(--spacing-sm);
}

.photo-card__actions {
    display: flex;
    gap: var(--spacing-sm);
}
```

### Form Components

#### Input Field
```razor
<div class="form-group">
    <label for="photoTitle" class="form-label">Photo Title</label>
    <input 
        type="text" 
        id="photoTitle" 
        class="form-control @(IsValid ? "valid" : "invalid")"
        @bind="PhotoTitle"
        placeholder="Enter photo title..." />
    @if (!IsValid)
    {
        <div class="validation-message">Please enter a valid title</div>
    }
</div>
```

```css
/* FormComponents.razor.css */
.form-group {
    margin-bottom: var(--spacing-md);
}

.form-label {
    display: block;
    font-weight: var(--font-weight-medium);
    color: var(--gray-700);
    margin-bottom: var(--spacing-xs);
    font-size: var(--font-size-sm);
}

.form-control {
    width: 100%;
    padding: var(--spacing-sm) var(--spacing-md);
    border: var(--border-width) solid var(--gray-300);
    border-radius: var(--border-radius);
    font-size: var(--font-size-base);
    transition: var(--transition-normal);
    background-color: white;
}

.form-control:focus {
    outline: none;
    border-color: var(--primary-blue);
    box-shadow: 0 0 0 3px rgba(27, 110, 194, 0.1);
}

.form-control.valid {
    border-color: var(--success);
}

.form-control.invalid {
    border-color: var(--error);
}

.validation-message {
    color: var(--error);
    font-size: var(--font-size-sm);
    margin-top: var(--spacing-xs);
}
```

### Navigation Components

#### Sidebar Navigation Item
```razor
<div class="nav-item">
    <NavLink class="nav-link @(IsActive ? "active" : "")" href="@Href">
        <span class="oi @Icon" aria-hidden="true"></span>
        @Text
    </NavLink>
</div>
```

```css
/* NavItem.razor.css */
.nav-item {
    margin: var(--spacing-xs) var(--spacing-sm);
}

.nav-link {
    display: flex;
    align-items: center;
    padding: var(--spacing-sm) var(--spacing-md);
    color: rgba(255, 255, 255, 0.8);
    text-decoration: none;
    border-radius: var(--border-radius);
    transition: var(--transition-normal);
    font-weight: var(--font-weight-medium);
}

.nav-link:hover {
    color: white;
    background-color: rgba(255, 255, 255, 0.1);
    text-decoration: none;
}

.nav-link.active {
    color: white;
    background-color: rgba(255, 255, 255, 0.2);
}

.nav-link .oi {
    margin-right: var(--spacing-sm);
    font-size: var(--font-size-base);
}
```

### Status Components

#### Status Badge
```razor
<span class="status-badge status-badge--@Status.ToLower()">
    @Status
</span>
```

```css
/* StatusBadge.razor.css */
.status-badge {
    display: inline-flex;
    align-items: center;
    padding: var(--spacing-xs) var(--spacing-sm);
    border-radius: var(--border-radius-full);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

.status-badge--success {
    background-color: var(--success-light);
    color: var(--success-dark);
}

.status-badge--error {
    background-color: var(--error-light);
    color: var(--error-dark);
}

.status-badge--warning {
    background-color: var(--warning-light);
    color: var(--warning-dark);
}

.status-badge--info {
    background-color: var(--info-light);
    color: var(--info-dark);
}
```

### Loading Components

#### Loading Spinner
```razor
<div class="loading-spinner @(Size)" aria-label="Loading..."></div>
```

```css
/* LoadingSpinner.razor.css */
.loading-spinner {
    border: 2px solid var(--gray-300);
    border-top: 2px solid var(--primary-blue);
    border-radius: 50%;
    animation: spin 1s linear infinite;
}

.loading-spinner.small {
    width: 16px;
    height: 16px;
}

.loading-spinner.medium {
    width: 24px;
    height: 24px;
}

.loading-spinner.large {
    width: 32px;
    height: 32px;
}

@keyframes spin {
    0% { transform: rotate(0deg); }
    100% { transform: rotate(360deg); }
}
```

### Modal Components

#### Modal Dialog
```razor
@if (IsVisible)
{
    <div class="modal-backdrop" @onclick="OnBackdropClick">
        <div class="modal-dialog" @onclick:stopPropagation="true">
            <div class="modal-header">
                <h3 class="modal-title">@Title</h3>
                <button class="modal-close" @onclick="OnClose">
                    <span class="oi oi-x" aria-hidden="true"></span>
                </button>
            </div>
            <div class="modal-body">
                @Body
            </div>
            <div class="modal-footer">
                @Footer
            </div>
        </div>
    </div>
}
```

```css
/* Modal.razor.css */
.modal-backdrop {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background-color: rgba(0, 0, 0, 0.5);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: var(--z-modal-backdrop);
}

.modal-dialog {
    background: white;
    border-radius: var(--border-radius-lg);
    box-shadow: var(--shadow-xl);
    max-width: 500px;
    width: 90%;
    max-height: 90vh;
    overflow: hidden;
    animation: modalSlideIn 0.3s ease-out;
}

.modal-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: var(--spacing-lg);
    border-bottom: var(--border-width) solid var(--gray-200);
}

.modal-title {
    margin: 0;
    font-size: var(--font-size-xl);
    font-weight: var(--font-weight-semibold);
    color: var(--gray-900);
}

.modal-close {
    background: none;
    border: none;
    font-size: var(--font-size-lg);
    color: var(--gray-600);
    cursor: pointer;
    padding: var(--spacing-xs);
    border-radius: var(--border-radius);
    transition: var(--transition-normal);
}

.modal-close:hover {
    background-color: var(--gray-100);
    color: var(--gray-900);
}

.modal-body {
    padding: var(--spacing-lg);
    overflow-y: auto;
}

.modal-footer {
    display: flex;
    justify-content: flex-end;
    gap: var(--spacing-sm);
    padding: var(--spacing-lg);
    border-top: var(--border-width) solid var(--gray-200);
    background-color: var(--gray-50);
}

@keyframes modalSlideIn {
    from {
        opacity: 0;
        transform: translateY(-20px) scale(0.95);
    }
    to {
        opacity: 1;
        transform: translateY(0) scale(1);
    }
}
```

## Utility Classes

### Spacing Utilities
```html
<!-- Margins -->
<div class="m-0">No margin</div>
<div class="m-xs">Extra small margin</div>
<div class="m-sm">Small margin</div>
<div class="m-md">Medium margin</div>
<div class="m-lg">Large margin</div>
<div class="m-xl">Extra large margin</div>

<!-- Padding -->
<div class="p-0">No padding</div>
<div class="p-xs">Extra small padding</div>
<div class="p-sm">Small padding</div>
<div class="p-md">Medium padding</div>
<div class="p-lg">Large padding</div>
<div class="p-xl">Extra large padding</div>
```

### Color Utilities
```html
<!-- Text Colors -->
<span class="text-primary">Primary text</span>
<span class="text-secondary">Secondary text</span>
<span class="text-success">Success text</span>
<span class="text-error">Error text</span>
<span class="text-warning">Warning text</span>
<span class="text-info">Info text</span>

<!-- Background Colors -->
<div class="bg-primary">Primary background</div>
<div class="bg-secondary">Secondary background</div>
<div class="bg-success">Success background</div>
<div class="bg-error">Error background</div>
<div class="bg-warning">Warning background</div>
<div class="bg-info">Info background</div>
```

### Border Radius Utilities
```html
<div class="rounded-sm">Small radius</div>
<div class="rounded">Default radius</div>
<div class="rounded-md">Medium radius</div>
<div class="rounded-lg">Large radius</div>
<div class="rounded-xl">Extra large radius</div>
<div class="rounded-full">Full radius (circle)</div>
```

### Shadow Utilities
```html
<div class="shadow-sm">Small shadow</div>
<div class="shadow">Default shadow</div>
<div class="shadow-md">Medium shadow</div>
<div class="shadow-lg">Large shadow</div>
<div class="shadow-xl">Extra large shadow</div>
```

### Transition Utilities
```html
<div class="transition">Normal transition</div>
<div class="transition-fast">Fast transition</div>
<div class="transition-slow">Slow transition</div>
```

## Best Practices

### 1. Use CSS Variables
Always use design system variables instead of hardcoded values:
```css
/* ✅ Good */
.my-component {
    color: var(--primary-blue);
    padding: var(--spacing-md);
}

/* ❌ Bad */
.my-component {
    color: #1b6ec2;
    padding: 16px;
}
```

### 2. Follow BEM Naming
Use BEM methodology for component CSS:
```css
.photo-card {}
.photo-card__image {}
.photo-card__title {}
.photo-card--featured {}
```

### 3. Responsive Design
Use the breakpoint variables for responsive design:
```css
@media (min-width: var(--breakpoint-md)) {
    .my-component {
        /* Desktop styles */
    }
}
```

### 4. Accessibility
Always include proper ARIA labels and focus states:
```razor
<button class="btn btn-primary" aria-label="Add new photo">
    <span class="oi oi-plus" aria-hidden="true"></span>
    Add Photo
</button>
```

### 5. Performance
Use CSS variables for dynamic theming and avoid inline styles:
```css
/* ✅ Good - CSS Variables */
.theme-dark {
    --primary-color: #4a90e2;
}

/* ❌ Bad - Inline Styles */
<div style="color: #4a90e2;">Content</div>
```

## Testing Your Components

### Visual Testing Checklist
- [ ] Component looks correct on desktop
- [ ] Component looks correct on mobile
- [ ] Hover states work properly
- [ ] Focus states are visible
- [ ] Colors meet contrast requirements
- [ ] Text is readable at all sizes

### Accessibility Testing Checklist
- [ ] Screen reader can navigate the component
- [ ] Keyboard navigation works
- [ ] Focus indicators are visible
- [ ] Color is not the only way to convey information
- [ ] Alt text is provided for images
- [ ] ARIA labels are used where appropriate

---

*This component library should be updated as new components are created and existing ones are modified.* 