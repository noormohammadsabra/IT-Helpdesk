# IT Help Desk & Ticketing Management System

Internship project for an internal IT Help Desk and Ticketing Management System.

## Week 3 Scope

Week 3 focuses on the application foundation:

- React frontend project setup.
- ASP.NET Core Web API backend setup.
- SQL Server Express database connection.
- JWT authentication.
- Role-based authorization.
- Login page and authenticated dashboard/index page.

## Week 4 Scope

Week 4 focuses on ticket management:

- Ticket category setup.
- Ticket creation.
- Ticket listing.
- Ticket editing and status updates.
- Ticket deletion.
- React frontend connected to ticket APIs.

## Technology Stack

| Layer | Tool |
|---|---|
| Frontend | React.js, Vite, Tailwind CSS |
| Backend | ASP.NET Core Web API |
| Database | SQL Server Express |
| API testing | Postman |
| Source control | GitHub |

## Project Structure

```text
IT-Helpdesk/
+-- frontend/   React user interface
+-- backend/    ASP.NET Core Web API
+-- database/   SQL setup scripts
+-- README.md
```

## Demo Users

All seeded demo accounts use this password:

```text
Password123!
```

| Role | Email |
|---|---|
| Admin | admin@ids.com |
| Agent | agent@ids.com |
| Manager | manager@ids.com |
| Employee | employee@ids.com |

## Run Backend

Open a terminal in the project root:

```bash
cd backend
dotnet run --urls http://127.0.0.1:5090
```

The API will create the `ITHelpDesk` database, roles, demo users, ticket lookups, and seed tickets automatically if they do not already exist.

## Run Frontend

Open another terminal in the project root:

```bash
cd frontend
npm install
npm run dev
```

Open:

```text
http://localhost:5173
```

## Main API Endpoints

| Method | Endpoint | Purpose | Protection |
|---|---|---|---|
| POST | `/api/auth/login` | Login and receive JWT token | Public |
| POST | `/api/auth/register` | Register a user | Public |
| GET | `/api/auth/me` | Current logged-in user | JWT required |
| GET | `/api/dashboard` | Dashboard data | JWT required |
| GET | `/api/admin/users` | User list | Admin role only |
| GET | `/api/ticket-categories` | Ticket categories | JWT required |
| GET | `/api/ticket-priorities` | Ticket priorities | JWT required |
| GET | `/api/ticket-statuses` | Ticket statuses | JWT required |
| GET | `/api/tickets` | List tickets | JWT required |
| POST | `/api/tickets` | Create ticket | JWT required |
| PUT | `/api/tickets/{id}` | Update ticket | JWT required |
| DELETE | `/api/tickets/{id}` | Delete ticket | JWT required |

## Postman Login Test

Request:

```http
POST http://127.0.0.1:5090/api/auth/login
Content-Type: application/json
```

Body:

```json
{
  "email": "admin@ids.com",
  "password": "Password123!"
}
```

Copy the returned token and use it as:

```text
Authorization: Bearer YOUR_TOKEN_HERE
```

## Role-Based Authorization Test

- Admin can access `/api/admin/users`.
- Employee can log in and open `/api/dashboard`.
- Employee receives `403 Forbidden` when trying to access `/api/admin/users`.

## Ticket CRUD Test

After login, use the JWT token to create a ticket:

```http
POST http://127.0.0.1:5090/api/tickets
Content-Type: application/json
Authorization: Bearer YOUR_TOKEN_HERE
```

Body:

```json
{
  "title": "Laptop battery issue",
  "description": "Battery drains very quickly and needs IT support.",
  "categoryId": 1,
  "priorityId": 2
}
```

Then list tickets:

```http
GET http://127.0.0.1:5090/api/tickets
Authorization: Bearer YOUR_TOKEN_HERE
```
