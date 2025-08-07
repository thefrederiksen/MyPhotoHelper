/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,js,razor,cshtml}",
    "./src/**/*.razor",
    "./src/MyPhotoHelper/**/*.razor",
    "./src/MyPhotoHelper/Components/**/*.razor",
    "./src/MyPhotoHelper/Pages/**/*.razor",
    "./src/MyPhotoHelper/Shared/**/*.razor"
  ],
  theme: {
    extend: {
      colors: {
        // Primary Colors - from variables.css
        primary: {
          DEFAULT: '#0B4F71',
          dark: '#073D58',
          hover: '#0D5A81',
          light: '#E0E7FF',
        },
        // Accent Colors
        accent: {
          blue: '#2196F3',
          green: '#4CAF50',
          orange: '#FF9800',
          red: '#F44336',
        },
        // Secondary Colors
        purple: {
          DEFAULT: '#3a0647',
          dark: '#2d004d',
          light: '#4a0a5a',
        },
        // Neutral Colors - Drata style
        gray: {
          50: '#F5F7FA',
          100: '#FFFFFF',
          200: '#E0E6ED',
          300: '#D1D5DB',
          400: '#8792A2',
          500: '#647788',
          600: '#6B7280',
          700: '#1A1F36',
          800: '#111827',
          900: '#000000',
        },
        // Status Colors
        success: {
          DEFAULT: '#26b050',
          light: '#d4edda',
          dark: '#1e7e34',
        },
        error: {
          DEFAULT: '#dc3545',
          light: '#f8d7da',
          dark: '#c82333',
        },
        warning: {
          DEFAULT: '#ffc107',
          light: '#fff3cd',
          dark: '#e0a800',
        },
        info: {
          DEFAULT: '#17a2b8',
          light: '#d1ecf1',
          dark: '#138496',
        },
      },
      fontFamily: {
        sans: ['Inter', '-apple-system', 'BlinkMacSystemFont', 'Segoe UI', 'Roboto', 'sans-serif'],
        mono: ['Fira Code', 'SFMono-Regular', 'Consolas', 'Liberation Mono', 'Menlo', 'monospace'],
      },
      fontSize: {
        xs: '0.75rem',     // 12px
        sm: '0.875rem',    // 14px
        base: '1rem',      // 16px
        lg: '1.125rem',    // 18px
        xl: '1.25rem',     // 20px
        '2xl': '1.5rem',   // 24px
        '3xl': '1.875rem', // 30px
        '4xl': '2.25rem',  // 36px
        '5xl': '3rem',     // 48px
      },
      spacing: {
        'xs': '0.25rem',   // 4px
        'sm': '0.5rem',    // 8px
        'md': '1rem',      // 16px
        'lg': '1.5rem',    // 24px
        'xl': '2rem',      // 32px
        '2xl': '3rem',     // 48px
        '3xl': '4rem',     // 64px
      },
      borderRadius: {
        sm: '0.25rem',   // 4px
        DEFAULT: '0.375rem', // 6px
        md: '0.5rem',    // 8px
        lg: '0.75rem',   // 12px
        xl: '1rem',      // 16px
      },
      boxShadow: {
        sm: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
        DEFAULT: '0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px 0 rgba(0, 0, 0, 0.06)',
        md: '0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)',
        lg: '0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)',
        xl: '0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)',
        card: '0 1px 3px rgba(0, 0, 0, 0.05)',
      },
      transitionDuration: {
        fast: '150ms',
        normal: '250ms',
        slow: '350ms',
      },
      maxWidth: {
        container: '1200px',
      },
      width: {
        sidebar: '220px',
      },
      height: {
        header: '4rem',
      },
    },
  },
  plugins: [
    require('@tailwindcss/forms'),
  ],
}