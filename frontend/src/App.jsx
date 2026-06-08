import { useState } from 'react'
import './App.css'

const API_BASE_URL = 'http://127.0.0.1:5090'

const demoAccounts = [
  { role: 'Admin', email: 'admin@ids.com' },
  { role: 'Agent', email: 'agent@ids.com' },
  { role: 'Manager', email: 'manager@ids.com' },
  { role: 'Employee', email: 'employee@ids.com' },
]

function App() {
  const [email, setEmail] = useState('employee@ids.com')
  const [password, setPassword] = useState('Password123!')
  const [token, setToken] = useState('')
  const [user, setUser] = useState(null)
  const [dashboard, setDashboard] = useState(null)
  const [adminUsers, setAdminUsers] = useState([])
  const [error, setError] = useState('')
  const [isLoading, setIsLoading] = useState(false)

  const login = async (event) => {
    event.preventDefault()
    setError('')
    setIsLoading(true)
    setAdminUsers([])

    try {
      const loginResponse = await fetch(`${API_BASE_URL}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      })

      if (!loginResponse.ok) {
        throw new Error('Invalid email or password.')
      }

      const loginData = await loginResponse.json()
      setToken(loginData.token)
      setUser(loginData)

      const dashboardResponse = await fetch(`${API_BASE_URL}/api/dashboard`, {
        headers: { Authorization: `Bearer ${loginData.token}` },
      })

      if (!dashboardResponse.ok) {
        throw new Error('Login worked, but dashboard data could not be loaded.')
      }

      setDashboard(await dashboardResponse.json())
    } catch (err) {
      setError(err.message)
      setToken('')
      setUser(null)
      setDashboard(null)
    } finally {
      setIsLoading(false)
    }
  }

  const loadAdminUsers = async () => {
    setError('')

    try {
      const response = await fetch(`${API_BASE_URL}/api/admin/users`, {
        headers: { Authorization: `Bearer ${token}` },
      })

      if (response.status === 403) {
        throw new Error('Your role is not allowed to open the admin users list.')
      }

      if (!response.ok) {
        throw new Error('Admin users could not be loaded.')
      }

      const data = await response.json()
      setAdminUsers(data.value ?? data)
    } catch (err) {
      setAdminUsers([])
      setError(err.message)
    }
  }

  const logout = () => {
    setToken('')
    setUser(null)
    setDashboard(null)
    setAdminUsers([])
    setError('')
  }

  if (user && dashboard) {
    return (
      <main className="min-h-screen bg-[#f6f8fb] text-slate-900">
        <div className="flex min-h-screen">
          <aside className="hidden w-64 border-r border-slate-200 bg-white px-5 py-6 md:block">
            <div className="mb-8">
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                IDS Internal
              </p>
              <h1 className="mt-1 text-xl font-bold text-slate-950">IT Help Desk</h1>
            </div>

            <nav className="space-y-1 text-sm font-medium">
              {['Dashboard', 'Tickets', 'Create Ticket', 'Reports', 'Profile'].map((item) => (
                <button
                  className="block w-full rounded-md px-3 py-2 text-left text-slate-700 hover:bg-slate-100"
                  key={item}
                  type="button"
                >
                  {item}
                </button>
              ))}
            </nav>
          </aside>

          <section className="flex-1 px-5 py-6 sm:px-8">
            <header className="mb-8 flex flex-col gap-4 border-b border-slate-200 pb-5 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="text-sm font-medium text-slate-500">Authenticated dashboard</p>
                <h2 className="text-2xl font-bold text-slate-950">Welcome, {user.fullName}</h2>
              </div>

              <div className="flex flex-wrap items-center gap-3">
                <div className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm">
                  <span className="font-semibold text-slate-950">{user.role}</span>
                  <span className="ml-2 text-slate-500">{user.department}</span>
                </div>
                <button
                  className="rounded-md bg-slate-950 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800"
                  onClick={logout}
                  type="button"
                >
                  Logout
                </button>
              </div>
            </header>

            {error && (
              <div className="mb-5 rounded-md border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
                {error}
              </div>
            )}

            <div className="grid gap-4 sm:grid-cols-3">
              <article className="rounded-md border border-slate-200 bg-white p-5">
                <p className="text-sm font-medium text-slate-500">Open tickets</p>
                <p className="mt-2 text-3xl font-bold text-slate-950">{dashboard.openTickets}</p>
              </article>
              <article className="rounded-md border border-slate-200 bg-white p-5">
                <p className="text-sm font-medium text-slate-500">In progress</p>
                <p className="mt-2 text-3xl font-bold text-slate-950">
                  {dashboard.inProgressTickets}
                </p>
              </article>
              <article className="rounded-md border border-slate-200 bg-white p-5">
                <p className="text-sm font-medium text-slate-500">Resolved</p>
                <p className="mt-2 text-3xl font-bold text-slate-950">
                  {dashboard.resolvedTickets}
                </p>
              </article>
            </div>

            <section className="mt-6 rounded-md border border-slate-200 bg-white">
              <div className="border-b border-slate-200 px-5 py-4">
                <h3 className="text-base font-semibold text-slate-950">Recent tickets</h3>
              </div>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[640px] text-left text-sm">
                  <thead className="bg-slate-50 text-slate-500">
                    <tr>
                      <th className="px-5 py-3 font-semibold">Reference</th>
                      <th className="px-5 py-3 font-semibold">Title</th>
                      <th className="px-5 py-3 font-semibold">Priority</th>
                      <th className="px-5 py-3 font-semibold">Status</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {dashboard.recentTickets.map((ticket) => (
                      <tr key={ticket.reference}>
                        <td className="px-5 py-4 font-medium text-slate-950">
                          {ticket.reference}
                        </td>
                        <td className="px-5 py-4">{ticket.title}</td>
                        <td className="px-5 py-4">{ticket.priority}</td>
                        <td className="px-5 py-4">{ticket.status}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>

            <section className="mt-6 rounded-md border border-slate-200 bg-white p-5">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <h3 className="text-base font-semibold text-slate-950">Role-based access test</h3>
                  <p className="mt-1 text-sm text-slate-500">
                    Only Admin users can load the protected user list.
                  </p>
                </div>
                <button
                  className="rounded-md bg-[#0f6b6e] px-4 py-2 text-sm font-bold text-white hover:bg-[#0b575a]"
                  onClick={loadAdminUsers}
                  type="button"
                >
                  Load admin users
                </button>
              </div>

              {adminUsers.length > 0 && (
                <div className="mt-5 overflow-x-auto">
                  <table className="w-full min-w-[640px] text-left text-sm">
                    <thead className="bg-slate-50 text-slate-500">
                      <tr>
                        <th className="px-4 py-3 font-semibold">Name</th>
                        <th className="px-4 py-3 font-semibold">Email</th>
                        <th className="px-4 py-3 font-semibold">Role</th>
                        <th className="px-4 py-3 font-semibold">Department</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {adminUsers.map((adminUser) => (
                        <tr key={adminUser.id}>
                          <td className="px-4 py-3 font-medium text-slate-950">
                            {adminUser.fullName}
                          </td>
                          <td className="px-4 py-3">{adminUser.email}</td>
                          <td className="px-4 py-3">{adminUser.roleName}</td>
                          <td className="px-4 py-3">{adminUser.department}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </section>
          </section>
        </div>
      </main>
    )
  }

  return (
    <main className="grid min-h-screen bg-[#f6f8fb] text-slate-900 lg:grid-cols-[1fr_460px]">
      <section className="flex items-center px-6 py-10 sm:px-10 lg:px-16">
        <div className="max-w-3xl">
          <p className="text-sm font-semibold uppercase tracking-wide text-[#0f6b6e]">
            IDS Internal Support
          </p>
          <h1 className="mt-4 text-4xl font-bold text-slate-950 sm:text-5xl">
            IT Help Desk & Ticketing Management System
          </h1>
          <p className="mt-5 max-w-2xl text-base leading-7 text-slate-600">
            Centralized support access for employees, support agents, managers, and
            administrators.
          </p>

          <div className="mt-8 grid gap-4 sm:grid-cols-3">
            <div className="rounded-md border border-slate-200 bg-white p-4">
              <p className="text-sm font-medium text-slate-500">Open tickets</p>
              <p className="mt-2 text-2xl font-bold text-slate-950">18</p>
            </div>
            <div className="rounded-md border border-slate-200 bg-white p-4">
              <p className="text-sm font-medium text-slate-500">In progress</p>
              <p className="mt-2 text-2xl font-bold text-slate-950">7</p>
            </div>
            <div className="rounded-md border border-slate-200 bg-white p-4">
              <p className="text-sm font-medium text-slate-500">Resolved</p>
              <p className="mt-2 text-2xl font-bold text-slate-950">25</p>
            </div>
          </div>
        </div>
      </section>

      <section className="flex items-center border-l border-slate-200 bg-white px-6 py-10 sm:px-10">
        <form className="w-full" onSubmit={login}>
          <div className="mb-8">
            <h2 className="text-2xl font-bold text-slate-950">Sign in</h2>
            <p className="mt-2 text-sm text-slate-500">
              Use one seeded account. All passwords are <strong>Password123!</strong>
            </p>
          </div>

          <div className="mb-5 grid grid-cols-2 gap-2">
            {demoAccounts.map((account) => (
              <button
                className="rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-left text-xs font-semibold text-slate-700 hover:bg-slate-100"
                key={account.email}
                onClick={() => setEmail(account.email)}
                type="button"
              >
                {account.role}
              </button>
            ))}
          </div>

          <label className="block text-sm font-semibold text-slate-700" htmlFor="email">
            Email address
          </label>
          <input
            className="mt-2 w-full rounded-md border border-slate-300 px-3 py-3 text-sm outline-none focus:border-[#0f6b6e] focus:ring-2 focus:ring-[#0f6b6e]/15"
            id="email"
            name="email"
            onChange={(event) => setEmail(event.target.value)}
            type="email"
            value={email}
          />

          <label className="mt-5 block text-sm font-semibold text-slate-700" htmlFor="password">
            Password
          </label>
          <input
            className="mt-2 w-full rounded-md border border-slate-300 px-3 py-3 text-sm outline-none focus:border-[#0f6b6e] focus:ring-2 focus:ring-[#0f6b6e]/15"
            id="password"
            name="password"
            onChange={(event) => setPassword(event.target.value)}
            type="password"
            value={password}
          />

          {error && (
            <div className="mt-5 rounded-md border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
              {error}
            </div>
          )}

          <button
            className="mt-6 w-full rounded-md bg-[#0f6b6e] px-4 py-3 text-sm font-bold text-white hover:bg-[#0b575a] disabled:cursor-not-allowed disabled:opacity-70"
            disabled={isLoading}
            type="submit"
          >
            {isLoading ? 'Logging in...' : 'Login'}
          </button>
        </form>
      </section>
    </main>
  )
}

export default App
