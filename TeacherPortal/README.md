# CCS Teacher Portal

A Web-Based Teacher Portal for Student Information, Attendance, and Grade Monitoring — built with ASP.NET Core MVC, EF Core, PostgreSQL, and ASP.NET Core Identity.

## Features
- **Teacher**: register/login, manage Sections, Subjects, Students (auto-creates Student account), record attendance (single entry or full sheet), enter grades with auto-computed Final Grade, view reports.
- **Student**: login (view-only), see enrolled subjects, grades, attendance, remarks, profile.
- Role-based authorization (Teacher / Student). Each teacher only sees their own data.

## Tech Stack
- ASP.NET Core MVC (.NET 8)
- Entity Framework Core 8 + Npgsql (PostgreSQL)
- ASP.NET Core Identity
- Razor Views + custom CSS (CCS red / charcoal / gold theme)

## 1. Install packages
```bash
dotnet restore
dotnet tool install --global dotnet-ef        # if not installed
```

## 2. Configure PostgreSQL
Create a database called `TeacherPortalDB` (or let the migration create it):
```sql
CREATE DATABASE "TeacherPortalDB";
```
Update the connection string in `appsettings.json`:
```json
"DefaultConnection": "Host=localhost;Port=5432;Database=TeacherPortalDB;Username=postgres;Password=YOUR_PASSWORD_HERE"
```

## 3. Run migrations
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```
(The app also runs `Database.MigrateAsync()` automatically on startup and seeds sample data.)

## 4. Run the app
```bash
dotnet run
```
Open https://localhost:5001

## Sample Credentials
| Role    | Email              | Password     |
|---------|--------------------|--------------|
| Teacher | teacher@ccs.edu    | Teacher@123  |
| Student | juan@ccs.edu       | Student@123  |
| Student | ana@ccs.edu        | Student@123  |
| Student | mark@ccs.edu       | Student@123  |
| Student | liza@ccs.edu       | Student@123  |

## Project Structure
```
TeacherPortal/
├─ Controllers/         # Account, TeacherDashboard, StudentDashboard, Students, Sections, Subjects, Attendance, Grades, Reports
├─ Data/                # ApplicationDbContext, DbSeeder
├─ Models/              # Entities + ViewModels
├─ Views/               # Razor views per controller + Shared layouts
├─ wwwroot/css/         # theme.css (palette) + site.css (layout/components)
├─ Program.cs           # DI, Identity, EF wiring
├─ appsettings.json     # Connection string
└─ TeacherPortal.csproj
```

## Grade Formula
`FinalGrade = (Quiz + Activity + Assignment + Midterm + FinalExam) × 0.20`
- ≥ 75 → **Passed** (green badge)
- < 75 → **Failed** (red badge)

## UI / Theme Notes
The interface uses the **College of Computer Studies** color palette:
- **Primary Red `#B5121B`** — active nav, primary buttons, highlights
- **Charcoal `#3A3A3C`** — sidebar, header emphasis
- **Accent Gold `#E3B322`** — small accents and badges
- **Background `#F7F8FA`**, **Surface `#FFFFFF`**, **Text `#1F2937`**

Layout inspiration from the reference dashboard:
- Fixed left sidebar + sticky top header
- Hero welcome banner
- 4-up stat cards
- Two-column panels for recent attendance / recent grade updates
- Clean tables, pill badges, soft shadows, rounded corners

The Student portal uses a lighter top-nav layout for a clean, view-only feel.
