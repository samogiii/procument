# Frontend Documentation for Procument

## Overview
The frontend of the Procument application is built using **Vue.js** with **Nuxt** as the framework for server-side rendering and routing. It utilizes **Vuetify** for UI components and follows a dark theme design. The application is structured under the `client/app` directory.

## Key Technologies
- **Vue.js**: Core framework for building reactive components.
- **Nuxt**: Framework for SSR, routing, and project structure.
- **Vuetify**: UI component library with Material Design principles.
- **Pinia**: State management library for Vue.

## Project Structure
- **`client/app/app.vue`**: Root component of the application.
- **`client/app/layouts/default.vue`**: Default layout with navigation drawer and app bar.
- **`client/app/pages/`**: Contains page components like RFQs and user management.
- **`client/app/composables/`**: Reusable logic like API fetching with `useApi.ts`.
- **`client/app/stores/`**: Pinia stores for state management, e.g., `auth.ts` for user authentication.
- **`client/vuetify.config.ts`**: Configuration for Vuetify themes and component defaults.

## Key Components
### Layouts
- **Default Layout** (`layouts/default.vue`): 
  - Features a collapsible navigation drawer with logo, menu items, and user profile.
  - Includes an app bar with page title and action icons.
  - Provides admin-specific navigation options.

### Pages
- **RFQs Page** (`pages/rfqs/index.vue`):
  - Displays a list of RFQs with search functionality.
  - Allows creation of new RFQs via a modal with form validation.
  - Features autocomplete for customer and part number fields.

## State Management
- **Auth Store** (`stores/auth.ts`):
  - Manages user authentication state with token, role, and user details.
  - Provides getters for authentication status and user initials.
  - Persists user data in local storage.

## API Integration
- **API Utility** (`composables/useApi.ts`):
  - Custom composable for API requests with methods for GET, POST, PUT, PATCH, and DELETE.
  - Automatically includes authentication headers when a user token is available.

## Theming
- **Vuetify Configuration** (`vuetify.config.ts`):
  - Defines a custom dark theme named `procumentDark` with specific color schemes.
  - Sets global defaults for components like cards, buttons, and input fields to ensure consistent styling.

## User Interface Features
- Responsive navigation drawer that can be collapsed to a rail view.
- Data tables for displaying lists with server-side pagination.
- Modal dialogs for form inputs with autocomplete functionality.
- Custom icons and branding elements like the Procument logo.

## Development Notes
- The frontend is tightly integrated with a backend API, accessed via the `useApi` composable.
- Authentication plays a significant role in UI rendering, with conditional elements based on user roles.
- The application emphasizes user experience with a modern dark theme and interactive components.
