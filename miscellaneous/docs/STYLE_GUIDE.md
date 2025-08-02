# MyPhotoHelper Style Guide

## Table of Contents
1. [Brand Identity](#brand-identity)
2. [Color Palette](#color-palette)
3. [Typography](#typography)
4. [Layout & Spacing](#layout--spacing)
5. [Components](#components)
6. [Interactive Elements](#interactive-elements)
7. [Responsive Design](#responsive-design)
8. [Accessibility](#accessibility)
9. [Implementation Guidelines](#implementation-guidelines)

## Brand Identity

### Application Name
- **Primary**: MyPhotoHelper
- **Alternative**: FaceVault (used in some UI elements)
- **Tagline**: (To be defined)

### Design Philosophy
- **Clean & Modern**: Minimalist design with focus on content
- **Photo-Centric**: UI that highlights and celebrates photography
- **Professional**: Suitable for both personal and professional photo management
- **Accessible**: Inclusive design for all users

## Color Palette

### Primary Colors
```css
/* Primary Blue */
--primary-blue: #1b6ec2;
--primary-blue-dark: #1861ac;
--primary-blue-light: #258cfb;

/* Primary Purple (Sidebar) */
--primary-purple: #3a0647;
--primary-purple-dark: #2d004d;
```

### Secondary Colors
```css
/* Neutral Grays */
--gray-100: #f7f7f7;
--gray-200: #d6d5d5;
--gray-300: #e9ecef;
--gray-600: #6c757d;
--gray-900: #212529;

/* Status Colors */
--success: #26b050;
--error: #dc3545;
--warning: #ffc107;
--info: #17a2b8;
```

### Usage Guidelines
- **Primary Blue**: Buttons, links, primary actions
- **Primary Purple**: Sidebar background, brand elements
- **Gray-100**: Top row background, card backgrounds
- **Gray-200**: Borders, dividers
- **Success**: Validation success, positive feedback
- **Error**: Validation errors, destructive actions
- **Warning**: Caution states, pending actions
- **Info**: Informational messages, help text

## Typography

### Font Family
```css
font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif;
```

### Font Sizes
```css
/* Headings */
h1: 2.5rem (40px)
h2: 2rem (32px)
h3: 1.75rem (28px)
h4: 1.5rem (24px)
h5: 1.25rem (20px)
h6: 1rem (16px)

/* Body Text */
body: 1rem (16px)
small: 0.875rem (14px)
```

### Font Weights
- **Normal**: 400
- **Medium**: 500
- **Bold**: 700

### Line Heights
- **Headings**: 1.2
- **Body Text**: 1.5
- **Small Text**: 1.4

## Layout & Spacing

### Grid System
- **Container**: Bootstrap-based responsive grid
- **Sidebar Width**: 250px (desktop)
- **Main Content**: Flexible width
- **Gutters**: 1rem (16px) standard

### Spacing Scale
```css
/* Spacing Units */
--spacing-xs: 0.25rem (4px)
--spacing-sm: 0.5rem (8px)
--spacing-md: 1rem (16px)
--spacing-lg: 1.5rem (24px)
--spacing-xl: 2rem (32px)
--spacing-xxl: 3rem (48px)
```

### Layout Structure
```css
/* Page Layout */
.page {
    display: flex;
    flex-direction: column;
}

/* Desktop Layout */
@media (min-width: 641px) {
    .page {
        flex-direction: row;
    }
    
    .sidebar {
        width: 250px;
        height: 100vh;
        position: sticky;
        top: 0;
    }
}
```

## Components

### Navigation
```css
/* Sidebar Navigation */
.sidebar {
    background-image: linear-gradient(180deg, rgb(5, 39, 103) 0%, #3a0647 70%);
}

.nav-link {
    color: rgba(255, 255, 255, 0.8);
    padding: 0.75rem 1rem;
    border-radius: 0.375rem;
    margin: 0.125rem 0.5rem;
}

.nav-link:hover {
    color: white;
    background-color: rgba(255, 255, 255, 0.1);
}

.nav-link.active {
    color: white;
    background-color: rgba(255, 255, 255, 0.2);
}
```

### Buttons
```css
/* Primary Button */
.btn-primary {
    color: #fff;
    background-color: #1b6ec2;
    border-color: #1861ac;
    padding: 0.5rem 1rem;
    border-radius: 0.375rem;
    font-weight: 500;
}

.btn-primary:hover {
    background-color: #1861ac;
    border-color: #155a9e;
}

/* Secondary Button */
.btn-secondary {
    color: #6c757d;
    background-color: #fff;
    border-color: #6c757d;
    padding: 0.5rem 1rem;
    border-radius: 0.375rem;
    font-weight: 500;
}

/* Focus States */
.btn:focus {
    box-shadow: 0 0 0 0.1rem white, 0 0 0 0.25rem #258cfb;
}
```

### Cards
```css
/* Photo Card */
.photo-card {
    background: white;
    border: 1px solid #d6d5d5;
    border-radius: 0.5rem;
    padding: 1rem;
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
    transition: box-shadow 0.2s ease;
}

.photo-card:hover {
    box-shadow: 0 4px 8px rgba(0, 0, 0, 0.15);
}
```

### Forms
```css
/* Form Controls */
.form-control {
    border: 1px solid #d6d5d5;
    border-radius: 0.375rem;
    padding: 0.5rem 0.75rem;
    font-size: 1rem;
}

.form-control:focus {
    border-color: #258cfb;
    box-shadow: 0 0 0 0.1rem white, 0 0 0 0.25rem #258cfb;
}

/* Validation States */
.form-control.valid {
    border-color: #26b050;
}

.form-control.invalid {
    border-color: #dc3545;
}

.validation-message {
    color: #dc3545;
    font-size: 0.875rem;
    margin-top: 0.25rem;
}
```

## Interactive Elements

### Links
```css
/* Standard Links */
a {
    color: #0071c1;
    text-decoration: none;
}

a:hover {
    color: #005a9e;
    text-decoration: underline;
}

/* Button Links */
.btn-link {
    color: #0071c1;
    text-decoration: none;
    padding: 0;
    border: none;
    background: none;
}
```

### Icons
- **Icon Library**: Open Iconic
- **Icon Size**: 1rem (16px) standard
- **Icon Color**: Inherit from parent element
- **Icon Spacing**: 0.5rem margin-right

### Loading States
```css
/* Loading Spinner */
.loading-spinner {
    border: 2px solid #f3f3f3;
    border-top: 2px solid #1b6ec2;
    border-radius: 50%;
    width: 20px;
    height: 20px;
    animation: spin 1s linear infinite;
}

@keyframes spin {
    0% { transform: rotate(0deg); }
    100% { transform: rotate(360deg); }
}
```

## Responsive Design

### Breakpoints
```css
/* Mobile First Approach */
/* Extra Small: 0px - 640px */
/* Small: 641px - 768px */
/* Medium: 769px - 1024px */
/* Large: 1025px+ */

@media (max-width: 640.98px) {
    /* Mobile Styles */
    .sidebar {
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: auto;
        z-index: 1000;
    }
    
    .top-row:not(.auth) {
        display: none;
    }
}

@media (min-width: 641px) {
    /* Desktop Styles */
    .page {
        flex-direction: row;
    }
    
    .sidebar {
        width: 250px;
        height: 100vh;
        position: sticky;
        top: 0;
    }
}
```

### Mobile Considerations
- **Touch Targets**: Minimum 44px × 44px
- **Font Sizes**: Minimum 16px for body text
- **Spacing**: Increased padding for touch interfaces
- **Navigation**: Collapsible sidebar on mobile

## Accessibility

### Color Contrast
- **Normal Text**: Minimum 4.5:1 contrast ratio
- **Large Text**: Minimum 3:1 contrast ratio
- **UI Components**: Minimum 3:1 contrast ratio

### Focus Management
```css
/* Focus Indicators */
.btn:focus,
.form-control:focus,
.nav-link:focus {
    outline: 2px solid #258cfb;
    outline-offset: 2px;
}

/* Skip Links */
.skip-link {
    position: absolute;
    top: -40px;
    left: 6px;
    background: #1b6ec2;
    color: white;
    padding: 8px;
    text-decoration: none;
    z-index: 1001;
}

.skip-link:focus {
    top: 6px;
}
```

### Screen Reader Support
- **Alt Text**: All images must have descriptive alt text
- **ARIA Labels**: Use appropriate ARIA labels for interactive elements
- **Semantic HTML**: Use proper heading hierarchy and semantic elements
- **Landmarks**: Use ARIA landmarks for navigation and content areas

## Implementation Guidelines

### CSS Organization
1. **Global Styles**: `site.css`
2. **Component Styles**: Component-specific `.razor.css` files
3. **Layout Styles**: `MainLayout.razor.css`
4. **Utility Classes**: Bootstrap utilities + custom utilities

### Naming Conventions
```css
/* BEM Methodology */
.block {}
.block__element {}
.block--modifier {}

/* Examples */
.photo-card {}
.photo-card__image {}
.photo-card--featured {}
```

### CSS Variables
```css
:root {
    /* Colors */
    --primary-color: #1b6ec2;
    --secondary-color: #3a0647;
    
    /* Spacing */
    --spacing-unit: 1rem;
    
    /* Typography */
    --font-family-base: 'Helvetica Neue', Helvetica, Arial, sans-serif;
    --font-size-base: 1rem;
    --line-height-base: 1.5;
}
```

### Performance Guidelines
- **CSS Minification**: Use minified CSS in production
- **Critical CSS**: Inline critical styles in `<head>`
- **Lazy Loading**: Load non-critical CSS asynchronously
- **Image Optimization**: Use appropriate image formats and sizes

### Browser Support
- **Modern Browsers**: Chrome, Firefox, Safari, Edge (latest 2 versions)
- **Mobile Browsers**: iOS Safari, Chrome Mobile
- **Fallbacks**: Provide fallbacks for CSS Grid and Flexbox

## Maintenance

### Style Guide Updates
1. **Document Changes**: Update this guide when making design changes
2. **Version Control**: Track changes in version control
3. **Team Review**: Review changes with the development team
4. **Testing**: Test changes across different devices and browsers

### Quality Assurance
- **Linting**: Use CSS linting tools
- **Validation**: Validate CSS syntax
- **Cross-browser Testing**: Test in multiple browsers
- **Accessibility Testing**: Use accessibility testing tools

---

*This style guide should be treated as a living document and updated as the application evolves.* 