# Deployment Guide

This document explains how to run and deploy the IT Help Desk & Ticketing Management System.

## Deployment Status

The project is prepared for deployment. A real public deployment requires hosting accounts such as Azure, IIS hosting, Vercel, Netlify, or a company server. Because hosting credentials are private and were not provided, the submitted deployment work includes:

- Frontend production build configuration.
- Backend publish instructions.
- SQL Server production configuration example.
- Environment variable setup.
- Hosting plan for frontend, backend, and database.
- Local deployment/demo instructions.
- Smoke tests after deployment.

## Local Demo Deployment

Use this for the internship demo on the laptop.

### 1. Start SQL Server

Make sure this Windows service is running:

```text
SQL Server (SQLEXPRESS)
```

If the backend times out while connecting to SQL Server, restart this service.

### 2. Run Backend

```powershell
cd C:\Users\Lenovo\Documents\internship\backend
dotnet build --no-restore
dotnet run --no-build --urls http://127.0.0.1:5090
```

Expected result:

```text
Now listening on: http://127.0.0.1:5090
```

Backend smoke test:

```text
http://127.0.0.1:5090/api/tickets
```

Expected result without login:

```text
HTTP ERROR 401
```

This is correct because the API is protected.

### 3. Run Frontend

Open a second terminal:

```powershell
cd C:\Users\Lenovo\Documents\internship\frontend
npm install
npm run dev
```

Open:

```text
http://localhost:5173
```

### 4. Demo Users

All seeded users use this password:

```text
Password123!
```

| Role | Email |
|---|---|
| Admin | admin@ids.com |
| Agent | agent@ids.com |
| Manager | manager@ids.com |
| Employee | employee@ids.com |

## Production Frontend Deployment

The frontend is a React/Vite application.

### Build Command

```powershell
cd frontend
npm install
npm run build
```

The production files are created in:

```text
frontend/dist
```

### Hosting Options

You can host the frontend on:

- Vercel
- Netlify
- Azure Static Web Apps
- IIS static website
- Any static file host

### Frontend Environment Variable

Create a production environment variable:

```text
VITE_API_BASE_URL=https://YOUR_BACKEND_DOMAIN
```

Example:

```text
VITE_API_BASE_URL=https://ithelpdesk-api.azurewebsites.net
```

The frontend reads this value from:

```text
import.meta.env.VITE_API_BASE_URL
```

If the variable is missing, the frontend uses the local default:

```text
http://127.0.0.1:5090
```

## Production Backend Deployment

The backend is an ASP.NET Core Web API.

### Publish Command

```powershell
cd backend
dotnet publish -c Release -o publish
```

The deployable backend output is created in:

```text
backend/publish
```

### Hosting Options

You can host the backend on:

- Azure App Service
- IIS
- Windows Server
- Company internal server

### Backend Production Settings

Use this example file as a guide:

```text
backend/appsettings.Production.example.json
```

Do not commit real production passwords or secret keys.

Production values should be stored as hosting environment variables:

```text
ConnectionStrings__MasterConnection
ConnectionStrings__DefaultConnection
Jwt__SecretKey
Jwt__Issuer
Jwt__Audience
```

## Database Deployment

The database is SQL Server.

Development database:

```text
SQL Server Express
Database: ITHelpDesk
```

Production database options:

- Azure SQL Database
- SQL Server on a company server
- SQL Server on a hosted Windows Server

The backend can automatically create required tables and seed demo data through:

```text
DatabaseInitializer.cs
```

For a real company deployment, seeded demo users should be replaced with real company users.

## CORS Configuration

The backend currently allows local frontend URLs:

```text
http://localhost:5173
http://127.0.0.1:5173
```

For production, add the deployed frontend URL in:

```text
backend/Program.cs
```

Example:

```csharp
.WithOrigins(
    "http://localhost:5173",
    "http://127.0.0.1:5173",
    "https://YOUR_FRONTEND_DOMAIN"
)
```

## Post-Deployment Smoke Tests

After deployment, test these endpoints:

### Login

```http
POST /api/auth/login
```

Expected result:

```text
JWT token returned
```

### Tickets

```http
GET /api/tickets
Authorization: Bearer YOUR_TOKEN
```

Expected result:

```text
Ticket list returned
```

### Dashboard

```http
GET /api/dashboard/analytics
Authorization: Bearer YOUR_TOKEN
```

Expected result:

```text
KPI and chart data returned
```

### Reports

```http
GET /api/reports/export/pdf
Authorization: Bearer YOUR_TOKEN
```

Expected result:

```text
PDF file downloaded
```

### AI Assistant

```http
POST /api/ai/ticket-analysis
Authorization: Bearer YOUR_TOKEN
```

Expected result:

```text
Suggested category, priority, summary, and troubleshooting returned
```

## Deployment Summary

The application is deployment-ready from a code and configuration perspective. The remaining step for a live public URL is to connect the repository to real hosting accounts and provide production database credentials.
