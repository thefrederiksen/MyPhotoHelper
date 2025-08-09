/** @type {import('tailwindcss').Config} */
module.exports = {
  // Important: This is a component library configuration
  // The host project should have its own Tailwind config for base styles
  prefix: '', // No prefix to maintain compatibility
  important: false, // Let host project control importance
  content: [
    // Include all files that will use these classes
    './components/**/*.css',
    './utilities/**/*.css',
    './myhelper.css',
    // Include paths to projects that will use this library
    '../../MyPhotoHelper/**/*.{razor,html,cshtml,cs}',
    '../../MyPhotoHelper/wwwroot/**/*.{html,js,css}',
    // Add paths for other projects as needed
    // '../../MyGithubHelper/**/*.{razor,html,cshtml,cs}',
    // '../../MyDataHelper/**/*.{razor,html,cshtml,cs}',
  ],
  theme: {
    extend: {
      colors: {
        // Custom color palette
        primary: {
          50: '#eff6ff',
          100: '#dbeafe',
          200: '#bfdbfe',
          300: '#93c5fd',
          400: '#60a5fa',
          500: '#3b82f6',
          600: '#2563eb',
          700: '#1d4ed8',
          800: '#1e40af',
          900: '#1e3a8a',
        },
        secondary: {
          50: '#f9fafb',
          100: '#f3f4f6',
          200: '#e5e7eb',
          300: '#d1d5db',
          400: '#9ca3af',
          500: '#6b7280',
          600: '#4b5563',
          700: '#374151',
          800: '#1f2937',
          900: '#111827',
        },
      },
      fontFamily: {
        sans: ['Inter', '-apple-system', 'BlinkMacSystemFont', 'Segoe UI', 'Roboto', 'sans-serif'],
        mono: ['Fira Code', 'SFMono-Regular', 'Consolas', 'Liberation Mono', 'Menlo', 'monospace'],
      },
      animation: {
        'fade-in': 'fadeIn 0.3s ease-out',
        'slide-in-right': 'slideInRight 0.3s ease-out',
        'slide-in-left': 'slideInLeft 0.3s ease-out',
        'slide-in-up': 'slideInUp 0.3s ease-out',
        'bounce-in': 'bounceIn 0.3s ease-out',
        'shimmer': 'shimmer 2s infinite',
      },
      keyframes: {
        fadeIn: {
          '0%': { opacity: '0', transform: 'translateY(10px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        slideInRight: {
          '0%': { transform: 'translateX(100%)' },
          '100%': { transform: 'translateX(0)' },
        },
        slideInLeft: {
          '0%': { transform: 'translateX(-100%)' },
          '100%': { transform: 'translateX(0)' },
        },
        slideInUp: {
          '0%': { transform: 'translateY(100%)' },
          '100%': { transform: 'translateY(0)' },
        },
        bounceIn: {
          '0%': { transform: 'scale(0.95)', opacity: '0' },
          '70%': { transform: 'scale(1.05)' },
          '100%': { transform: 'scale(1)', opacity: '1' },
        },
        shimmer: {
          '0%': { backgroundPosition: '-1000px 0' },
          '100%': { backgroundPosition: '1000px 0' },
        },
      },
      scale: {
        '102': '1.02',
      },
      zIndex: {
        '60': '60',
        '70': '70',
        '80': '80',
        '90': '90',
        '100': '100',
      },
      transitionDuration: {
        '400': '400ms',
        '500': '500ms',
        '600': '600ms',
      },
      borderWidth: {
        '3': '3px',
      },
      spacing: {
        '18': '4.5rem',
        '88': '22rem',
        '120': '30rem',
      },
      lineClamp: {
        7: '7',
        8: '8',
        9: '9',
        10: '10',
      },
      typography: (theme) => ({
        DEFAULT: {
          css: {
            color: theme('colors.gray.900'),
            maxWidth: 'none',
          },
        },
      }),
    },
  },
  plugins: [
    // Add any Tailwind plugins if needed
    // require('@tailwindcss/forms'),
    // require('@tailwindcss/typography'),
    // require('@tailwindcss/aspect-ratio'),
    // require('@tailwindcss/line-clamp'),
  ],
  // Ensure we don't purge component styles
  safelist: [
    // Button base and variants
    'btn', 'btn-primary', 'btn-danger', 'btn-secondary', 'btn-success', 'btn-warning',
    'btn-outline-primary', 'btn-outline-danger', 'btn-outline-secondary',
    'btn-sm', 'btn-lg', 'btn-xs', 'btn-block', 'btn-icon', 'btn-link', 'btn-ghost',
    'btn-loading', 'btn-icon-sm', 'delete-single-btn',
    // Backward compatibility button variants
    'btn-danger-primary', 'btn-danger-confirm', 'btn-danger-small', 'btn-outline-small',
    // Card variants
    'card', 'card-body', 'card-header', 'card-footer', 'card-title',
    'stat-card', 'stat-card-enhanced', 'duplicate-group-card', 'photo-card',
    'card-hover', 'card-bordered', 'card-compact',
    // Alert variants
    'alert', 'alert-primary', 'alert-danger', 'alert-success', 'alert-warning', 'alert-info',
    'alert-dismissible', 'alert-icon',
    // Badge variants
    'badge', 'badge-primary', 'badge-danger', 'badge-success', 'badge-warning', 'badge-info',
    'badge-sm', 'badge-lg', 'badge-outline',
    // Status badges
    'status-badge', 'pending', 'active', 'signed', 'expired', 'draft', 'completed',
    // Form elements
    'form-control', 'form-input', 'form-textarea', 'form-select', 'form-check',
    'form-check-input', 'form-check-label', 'form-label', 'form-group',
    'form-input-error', 'form-input-success',
    // Modal elements
    'modal', 'modal-backdrop', 'modal-content', 'modal-header', 'modal-body', 'modal-footer',
    'modal-title', 'modal-close',
    // Utility classes
    'spinner', 'spinner-sm', 'spinner-lg', 'skeleton', 'skeleton-text',
    'divider', 'divider-vertical', 'hover-scale', 'hover-lift',
    'file-diff-highlight', 'duplicate-photos-grid', 'duplicate-image-container',
    // Progress indicators
    'progress-bar', 'progress-fill',
    // Color modifiers for dynamic classes
    'blue', 'red', 'green', 'yellow', 'orange', 'purple', 'gray',
    // Text utilities
    'text-muted', 'text-small', 'text-tiny', 'text-truncate',
    // Layout utilities
    'container-responsive', 'photo-grid',
  ],
}