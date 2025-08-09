# MyHelper UI Component Library

A centralized Tailwind CSS component library providing consistent, reusable UI components for MyPhotoHelper and related projects.

## Overview

MyHelper UI is built on Tailwind CSS best practices using the `@apply` directive for component composition. It provides a comprehensive set of UI components including buttons, cards, forms, modals, alerts, and utility classes.

## Build Process

### Automatic Build (Recommended for MyPhotoHelper)
The component library is automatically built when you build the main MyPhotoHelper project. The .NET build process will:
1. Check if node_modules exists, and run `npm install` if needed
2. Run `npm run build` to compile the CSS
3. Copy the compiled CSS to the output directory

### Manual Build
1. Install dependencies (first time only):
```bash
cd src/MyHelper/MyHelper.UI
npm install
```

2. Build the CSS:
```bash
npm run build        # Production build (minified)
npm run dev          # Development build with watch mode
npm run build:watch  # Production build with watch mode
```

3. Import in your main CSS file:
```css
@import '../../../MyHelper/MyHelper.UI/dist/myhelper.min.css';
```

### Output
- Compiled CSS location: `dist/myhelper.min.css`
- The library assumes the host project includes Tailwind base styles
- Only components and utilities are included (no base styles)

### For Other Projects

1. Copy the entire `MyHelper.UI` folder to your project
2. Install dependencies and build
3. Import the compiled CSS in your project

## Components

### Buttons

```html
<!-- Primary Button -->
<button class="btn btn-primary">Click Me</button>

<!-- Danger Button -->
<button class="btn btn-danger">Delete</button>

<!-- Success Button -->
<button class="btn btn-success">Save</button>

<!-- Outline Button -->
<button class="btn btn-outline-primary">Learn More</button>

<!-- Size Variants -->
<button class="btn btn-primary btn-sm">Small</button>
<button class="btn btn-primary btn-lg">Large</button>
```

### Cards

```html
<!-- Basic Card -->
<div class="card">
  <div class="card-header">
    <h3 class="card-title">Card Title</h3>
  </div>
  <div class="card-body">
    Card content goes here
  </div>
</div>

<!-- Statistics Card -->
<div class="stat-card-enhanced blue">
  <div class="stat-icon-wrapper">
    <span>📊</span>
  </div>
  <div class="stat-content">
    <div class="stat-label">Total Photos</div>
    <div class="stat-value">1,234</div>
  </div>
</div>
```

### Forms

```html
<!-- Form Group -->
<div class="form-group">
  <label class="form-label">Email Address</label>
  <input type="email" class="form-control" placeholder="Enter email">
  <span class="form-text">We'll never share your email.</span>
</div>

<!-- Form Select -->
<div class="form-group">
  <label class="form-label">Choose Option</label>
  <select class="form-select">
    <option>Option 1</option>
    <option>Option 2</option>
  </select>
</div>

<!-- Checkbox -->
<div class="form-check">
  <input type="checkbox" class="form-check-input" id="check1">
  <label class="form-check-label" for="check1">Remember me</label>
</div>
```

### Modals

```html
<!-- Modal Structure -->
<div class="modal">
  <div class="modal-backdrop"></div>
  <div class="modal-dialog">
    <div class="modal-content">
      <div class="modal-header">
        <h3 class="modal-title">Modal Title</h3>
        <button class="modal-close">×</button>
      </div>
      <div class="modal-body">
        Modal content
      </div>
      <div class="modal-footer">
        <button class="btn btn-secondary">Cancel</button>
        <button class="btn btn-primary">Save</button>
      </div>
    </div>
  </div>
</div>
```

### Alerts

```html
<!-- Success Alert -->
<div class="alert alert-success">
  <strong>Success!</strong> Your changes have been saved.
</div>

<!-- Dismissible Alert -->
<div class="alert alert-warning alert-dismissible">
  <strong>Warning!</strong> Please review your input.
  <button class="alert-dismiss">×</button>
</div>

<!-- Badge -->
<span class="badge badge-primary">New</span>
<span class="badge badge-success">Active</span>
```

## Utility Classes

### Loading States
- `.loading` - Adds loading state to any element
- `.skeleton` - Creates skeleton loading effect
- `.spinner` - Displays a loading spinner

### Text Utilities
- `.text-truncate` - Truncate text with ellipsis
- `.text-muted` - Muted text color
- `.text-small` - Small text size

### Hover Effects
- `.hover-scale` - Scale on hover
- `.hover-lift` - Lift with shadow on hover
- `.hover-glow` - Glow effect on hover

### Animations
- `.animate-fade-in` - Fade in animation
- `.animate-slide-in-right` - Slide in from right
- `.animate-bounce-in` - Bounce in effect

## Color System

The library uses a consistent color system:
- **Primary**: Blue (buttons, links, focus states)
- **Success**: Green (success messages, confirmations)
- **Danger**: Red (destructive actions, errors)
- **Warning**: Yellow/Orange (warnings, cautions)
- **Info**: Cyan (informational messages)
- **Secondary**: Gray (secondary actions)

## Building and Development

### Development Mode
```bash
npm run dev
```
This watches for changes and rebuilds automatically without minification.

### Production Build
```bash
npm run build
```
Creates a minified production build in `dist/myhelper.min.css`.

### Watch Mode
```bash
npm run build:watch
```
Watches for changes and rebuilds the minified version.

## Customization

### Extending Components

Create custom variants by extending existing components:

```css
@layer components {
  .btn-custom {
    @apply btn bg-purple-600 hover:bg-purple-700 text-white;
  }
}
```

### Overriding Styles

Override default styles in your project's CSS:

```css
.btn-primary {
  /* Your custom styles */
}
```

## Best Practices

1. **Use semantic class names**: Choose component classes that describe the element's purpose
2. **Combine with utilities**: Use Tailwind utilities for one-off styling
3. **Maintain consistency**: Use the provided components across all pages
4. **Avoid inline styles**: Prefer component classes over inline Tailwind classes
5. **Test responsiveness**: Ensure components work on all screen sizes

## Migration Guide

### From Inline Tailwind Classes

Before:
```html
<button class="bg-blue-600 hover:bg-blue-700 text-white font-medium py-2 px-4 rounded-lg shadow-md hover:shadow-lg transform hover:-translate-y-0.5 active:translate-y-0 transition-all duration-200">
  Click Me
</button>
```

After:
```html
<button class="btn btn-primary">
  Click Me
</button>
```

### From Custom CSS

Replace custom button styles with MyHelper UI components:

Before (Duplicates.razor.css):
```css
.btn-danger-primary {
  /* Custom styles */
}
```

After:
```html
<button class="btn btn-danger">Delete</button>
```

## Browser Support

- Chrome (latest)
- Firefox (latest)
- Safari (latest)
- Edge (latest)

## Contributing

When adding new components:
1. Create component styles using `@apply` directive
2. Add to appropriate file in `components/` directory
3. Document usage in this README
4. Add examples to the documentation
5. Test across different browsers and screen sizes

## License

MIT License - See LICENSE file for details