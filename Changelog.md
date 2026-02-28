28.02.2026 - JWT Token authentication

### Backend (PROG3 API) — Auth Infrastructure

| File | Purpose |
|------|---------|
| ConversionJob.cs | Missing entity (fixed compilation) |
| ITokenService.cs | JWT token generation interface |
| IPasswordHasherService.cs | Password hashing interface |
| AuthResponseDto.cs | Auth response DTO |
| UserProfileDto.cs | User profile DTO |
| LoginCommand.cs | MediatR login handler |
| RegisterCommand.cs | MediatR register handler |
| GetCurrentUserQuery.cs | Get profile handler |
| UserRepository.cs | EF Core implementation |
| TokenService.cs | JWT token generation |
| PasswordHasherService.cs | BCrypt hashing |
| AuthController.cs | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` |
| Program.cs | Added JWT auth middleware, service registrations |
| appsettings.json files | Added `JwtSettings` section |
| .csproj files | Added `Microsoft.AspNetCore.Authentication.JwtBearer`, `BCrypt.Net-Next` |

### Frontend (Vue.js 3 SPA) — frontend

| File | Purpose |
|------|---------|
| stores/auth.ts | **Pinia auth store** — global user state, login/register/logout actions, localStorage persistence |
| api/client.ts | Axios instance with JWT interceptor (auto-attaches `Bearer` token) |
| api/auth.ts | Auth API service (login, register, getCurrentUser) |
| router/index.ts | Vue Router with **navigation guards** (`requiresAuth`, `guestOnly`) |
| components/LoginForm.vue | Login form with VeeValidate + Zod validation |
| components/RegisterForm.vue | Register form with password rules & confirmation |
| components/AppHeader.vue | Navigation with user status, dark mode toggle, responsive mobile menu |
| components/ErrorMessage.vue | Reusable error display |
| 8 views | Home, Login, Register, Dashboard, Upload, Viewer, Profile, 404 |

### Key behaviors matching Goal 1 criteria:
- **Login** → `POST /api/auth/login` → JWT token stored in Pinia + localStorage → redirect to Dashboard
- **Register** → `POST /api/auth/register` → JWT token stored → redirect to Dashboard
- **Logout** → clears all state in Pinia + localStorage → redirect to Home
- **Protected routes** (`/dashboard`, `/upload`, `/viewer/:id`, `/profile`) redirect unauthenticated users to `/login`
- **Guest-only routes** (`/login`, `/register`) redirect authenticated users to `/dashboard`
- Both backend (`dotnet build`) and frontend (`npm run build`) compile with **0 errors**
