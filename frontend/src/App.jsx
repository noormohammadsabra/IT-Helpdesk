import { useState } from 'react'
import './App.css'

const API_BASE_URL = 'http://127.0.0.1:5090'

const demoAccounts = [
  { role: 'Admin', email: 'admin@ids.com' },
  { role: 'Agent', email: 'agent@ids.com' },
  { role: 'Manager', email: 'manager@ids.com' },
  { role: 'Employee', email: 'employee@ids.com' },
]

const emptyForm = {
  title: '',
  description: '',
  categoryId: '',
  priorityId: '',
  statusId: '',
}

function App() {
  const [email, setEmail] = useState('employee@ids.com')
  const [password, setPassword] = useState('Password123!')
  const [token, setToken] = useState('')
  const [user, setUser] = useState(null)
  const [tickets, setTickets] = useState([])
  const [categories, setCategories] = useState([])
  const [priorities, setPriorities] = useState([])
  const [statuses, setStatuses] = useState([])
  const [form, setForm] = useState(emptyForm)
  const [editingTicketId, setEditingTicketId] = useState(null)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [isLoading, setIsLoading] = useState(false)

  const authHeaders = (authToken = token) => ({
    Authorization: `Bearer ${authToken}`,
  })

  const loadWorkspace = async (authToken) => {
    const [categoriesResponse, prioritiesResponse, statusesResponse, ticketsResponse] =
      await Promise.all([
        fetch(`${API_BASE_URL}/api/ticket-categories`, { headers: authHeaders(authToken) }),
        fetch(`${API_BASE_URL}/api/ticket-priorities`, { headers: authHeaders(authToken) }),
        fetch(`${API_BASE_URL}/api/ticket-statuses`, { headers: authHeaders(authToken) }),
        fetch(`${API_BASE_URL}/api/tickets`, { headers: authHeaders(authToken) }),
      ])

    if (!categoriesResponse.ok || !prioritiesResponse.ok || !statusesResponse.ok || !ticketsResponse.ok) {
      throw new Error('Ticket data could not be loaded from the backend.')
    }

    const loadedCategories = await categoriesResponse.json()
    const loadedPriorities = await prioritiesResponse.json()
    const loadedStatuses = await statusesResponse.json()
    const loadedTickets = await ticketsResponse.json()

    setCategories(loadedCategories)
    setPriorities(loadedPriorities)
    setStatuses(loadedStatuses)
    setTickets(loadedTickets)
    setForm({
      ...emptyForm,
      categoryId: loadedCategories[0]?.id ?? '',
      priorityId: loadedPriorities[1]?.id ?? loadedPriorities[0]?.id ?? '',
      statusId: loadedStatuses[0]?.id ?? '',
    })
  }

  const login = async (event) => {
    event.preventDefault()
    setError('')
    setSuccess('')
    setIsLoading(true)

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
      await loadWorkspace(loginData.token)
      setSuccess('Login successful. Ticket management is connected to the API.')
    } catch (err) {
      setError(err.message)
      logout(false)
    } finally {
      setIsLoading(false)
    }
  }

  const reloadTickets = async () => {
    const response = await fetch(`${API_BASE_URL}/api/tickets`, {
      headers: authHeaders(),
    })

    if (!response.ok) {
      throw new Error('Tickets could not be refreshed.')
    }

    setTickets(await response.json())
  }

  const saveTicket = async (event) => {
    event.preventDefault()
    setError('')
    setSuccess('')
    setIsLoading(true)

    try {
      const payload = {
        title: form.title,
        description: form.description,
        categoryId: Number(form.categoryId),
        priorityId: Number(form.priorityId),
      }

      let response

      if (editingTicketId) {
        response = await fetch(`${API_BASE_URL}/api/tickets/${editingTicketId}`, {
          method: 'PUT',
          headers: {
            ...authHeaders(),
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            ...payload,
            statusId: Number(form.statusId),
          }),
        })
      } else {
        response = await fetch(`${API_BASE_URL}/api/tickets`, {
          method: 'POST',
          headers: {
            ...authHeaders(),
            'Content-Type': 'application/json',
          },
          body: JSON.stringify(payload),
        })
      }

      if (!response.ok) {
        throw new Error('Ticket could not be saved.')
      }

      await reloadTickets()
      cancelEdit()
      setSuccess(editingTicketId ? 'Ticket updated successfully.' : 'Ticket created successfully.')
    } catch (err) {
      setError(err.message)
    } finally {
      setIsLoading(false)
    }
  }

  const editTicket = (ticket) => {
    setEditingTicketId(ticket.id)
    setForm({
      title: ticket.title,
      description: ticket.description,
      categoryId: ticket.categoryId,
      priorityId: ticket.priorityId,
      statusId: ticket.statusId,
    })
    setError('')
    setSuccess('')
  }

  const deleteTicket = async (ticketId) => {
    const confirmed = window.confirm('Delete this ticket?')
    if (!confirmed) {
      return
    }

    setError('')
    setSuccess('')
    setIsLoading(true)

    try {
      const response = await fetch(`${API_BASE_URL}/api/tickets/${ticketId}`, {
        method: 'DELETE',
        headers: authHeaders(),
      })

      if (!response.ok) {
        throw new Error('Ticket could not be deleted.')
      }

      await reloadTickets()
      setSuccess('Ticket deleted successfully.')
    } catch (err) {
      setError(err.message)
    } finally {
      setIsLoading(false)
    }
  }

  const cancelEdit = () => {
    setEditingTicketId(null)
    setForm({
      ...emptyForm,
      categoryId: categories[0]?.id ?? '',
      priorityId: priorities[1]?.id ?? priorities[0]?.id ?? '',
      statusId: statuses[0]?.id ?? '',
    })
  }

  const logout = (clearMessage = true) => {
    setToken('')
    setUser(null)
    setTickets([])
    setCategories([])
    setPriorities([])
    setStatuses([])
    setForm(emptyForm)
    setEditingTicketId(null)
    if (clearMessage) {
      setError('')
      setSuccess('')
    }
  }

  const countByStatus = (statusName) => tickets.filter((ticket) => ticket.statusName === statusName).length

  if (user) {
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
                <p className="text-sm font-medium text-slate-500">Ticket management</p>
                <h2 className="text-2xl font-bold text-slate-950">Welcome, {user.fullName}</h2>
              </div>

              <div className="flex flex-wrap items-center gap-3">
                <div className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm">
                  <span className="font-semibold text-slate-950">{user.role}</span>
                  <span className="ml-2 text-slate-500">{user.department}</span>
                </div>
                <button
                  className="rounded-md bg-slate-950 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800"
                  onClick={() => logout()}
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
            {success && (
              <div className="mb-5 rounded-md border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
                {success}
              </div>
            )}

            <div className="grid gap-4 sm:grid-cols-4">
              <SummaryCard label="Total tickets" value={tickets.length} />
              <SummaryCard label="Open" value={countByStatus('Open')} />
              <SummaryCard label="In progress" value={countByStatus('In Progress')} />
              <SummaryCard label="Resolved" value={countByStatus('Resolved')} />
            </div>

            <section className="mt-6 grid gap-6 xl:grid-cols-[380px_1fr]">
              <form className="rounded-md border border-slate-200 bg-white p-5" onSubmit={saveTicket}>
                <div className="mb-5">
                  <h3 className="text-base font-semibold text-slate-950">
                    {editingTicketId ? 'Edit ticket' : 'Create ticket'}
                  </h3>
                  <p className="mt-1 text-sm text-slate-500">
                    Tickets are saved in SQL Server through the ASP.NET Core API.
                  </p>
                </div>

                <label className="block text-sm font-semibold text-slate-700" htmlFor="title">
                  Title
                </label>
                <input
                  className="mt-2 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0f6b6e] focus:ring-2 focus:ring-[#0f6b6e]/15"
                  id="title"
                  onChange={(event) => setForm({ ...form, title: event.target.value })}
                  required
                  value={form.title}
                />

                <label className="mt-4 block text-sm font-semibold text-slate-700" htmlFor="description">
                  Description
                </label>
                <textarea
                  className="mt-2 min-h-28 w-full resize-y rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0f6b6e] focus:ring-2 focus:ring-[#0f6b6e]/15"
                  id="description"
                  onChange={(event) => setForm({ ...form, description: event.target.value })}
                  required
                  value={form.description}
                />

                <div className="mt-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-1">
                  <SelectField
                    label="Category"
                    onChange={(value) => setForm({ ...form, categoryId: value })}
                    options={categories}
                    value={form.categoryId}
                  />
                  <SelectField
                    label="Priority"
                    onChange={(value) => setForm({ ...form, priorityId: value })}
                    options={priorities}
                    value={form.priorityId}
                  />
                </div>

                {editingTicketId && (
                  <div className="mt-4">
                    <SelectField
                      label="Status"
                      onChange={(value) => setForm({ ...form, statusId: value })}
                      options={statuses}
                      value={form.statusId}
                    />
                  </div>
                )}

                <div className="mt-6 flex gap-3">
                  <button
                    className="rounded-md bg-[#0f6b6e] px-4 py-2 text-sm font-bold text-white hover:bg-[#0b575a] disabled:cursor-not-allowed disabled:opacity-70"
                    disabled={isLoading}
                    type="submit"
                  >
                    {editingTicketId ? 'Update ticket' : 'Create ticket'}
                  </button>
                  {editingTicketId && (
                    <button
                      className="rounded-md border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
                      onClick={cancelEdit}
                      type="button"
                    >
                      Cancel
                    </button>
                  )}
                </div>
              </form>

              <section className="rounded-md border border-slate-200 bg-white">
                <div className="border-b border-slate-200 px-5 py-4">
                  <h3 className="text-base font-semibold text-slate-950">Tickets</h3>
                  <p className="mt-1 text-sm text-slate-500">
                    Employees see their tickets. Admin, Agent, and Manager accounts see all tickets.
                  </p>
                </div>
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[900px] text-left text-sm">
                    <thead className="bg-slate-50 text-slate-500">
                      <tr>
                        <th className="px-5 py-3 font-semibold">Ref</th>
                        <th className="px-5 py-3 font-semibold">Title</th>
                        <th className="px-5 py-3 font-semibold">Category</th>
                        <th className="px-5 py-3 font-semibold">Priority</th>
                        <th className="px-5 py-3 font-semibold">Status</th>
                        <th className="px-5 py-3 font-semibold">Created by</th>
                        <th className="px-5 py-3 font-semibold">Actions</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {tickets.map((ticket) => (
                        <tr key={ticket.id}>
                          <td className="px-5 py-4 font-medium text-slate-950">
                            {ticket.ticketNumber}
                          </td>
                          <td className="px-5 py-4">
                            <p className="font-medium text-slate-950">{ticket.title}</p>
                            <p className="mt-1 max-w-sm truncate text-xs text-slate-500">
                              {ticket.description}
                            </p>
                          </td>
                          <td className="px-5 py-4">{ticket.categoryName}</td>
                          <td className="px-5 py-4">{ticket.priorityName}</td>
                          <td className="px-5 py-4">{ticket.statusName}</td>
                          <td className="px-5 py-4">{ticket.createdByName}</td>
                          <td className="px-5 py-4">
                            <div className="flex gap-2">
                              <button
                                className="rounded-md border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-50"
                                onClick={() => editTicket(ticket)}
                                type="button"
                              >
                                Edit
                              </button>
                              <button
                                className="rounded-md border border-rose-200 px-3 py-1.5 text-xs font-semibold text-rose-700 hover:bg-rose-50"
                                onClick={() => deleteTicket(ticket.id)}
                                type="button"
                              >
                                Delete
                              </button>
                            </div>
                          </td>
                        </tr>
                      ))}
                      {tickets.length === 0 && (
                        <tr>
                          <td className="px-5 py-8 text-center text-slate-500" colSpan="7">
                            No tickets found.
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </section>
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
            Secure login, ticket CRUD operations, category management, and role-based API
            access for internal support workflows.
          </p>
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

function SummaryCard({ label, value }) {
  return (
    <article className="rounded-md border border-slate-200 bg-white p-5">
      <p className="text-sm font-medium text-slate-500">{label}</p>
      <p className="mt-2 text-3xl font-bold text-slate-950">{value}</p>
    </article>
  )
}

function SelectField({ label, options, value, onChange }) {
  return (
    <label className="block text-sm font-semibold text-slate-700">
      {label}
      <select
        className="mt-2 w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-[#0f6b6e] focus:ring-2 focus:ring-[#0f6b6e]/15"
        onChange={(event) => onChange(event.target.value)}
        required
        value={value}
      >
        {options.map((option) => (
          <option key={option.id} value={option.id}>
            {option.name}
          </option>
        ))}
      </select>
    </label>
  )
}

export default App
