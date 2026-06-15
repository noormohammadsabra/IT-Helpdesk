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

const canManageWorkflow = (role) => ['Admin', 'Agent', 'Manager'].includes(role)

function App() {
  const [email, setEmail] = useState('employee@ids.com')
  const [password, setPassword] = useState('Password123!')
  const [token, setToken] = useState('')
  const [user, setUser] = useState(null)
  const [tickets, setTickets] = useState([])
  const [categories, setCategories] = useState([])
  const [priorities, setPriorities] = useState([])
  const [statuses, setStatuses] = useState([])
  const [agents, setAgents] = useState([])
  const [form, setForm] = useState(emptyForm)
  const [editingTicketId, setEditingTicketId] = useState(null)
  const [selectedTicket, setSelectedTicket] = useState(null)
  const [comments, setComments] = useState([])
  const [activity, setActivity] = useState([])
  const [commentText, setCommentText] = useState('')
  const [isInternal, setIsInternal] = useState(false)
  const [assignmentUserId, setAssignmentUserId] = useState('')
  const [workflowStatusId, setWorkflowStatusId] = useState('')
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [isLoading, setIsLoading] = useState(false)

  const authHeaders = (authToken = token) => ({
    Authorization: `Bearer ${authToken}`,
  })

  const loadWorkspace = async (authToken, role) => {
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
    let loadedAgents = []

    if (canManageWorkflow(role)) {
      const agentsResponse = await fetch(`${API_BASE_URL}/api/agents`, {
        headers: authHeaders(authToken),
      })
      loadedAgents = agentsResponse.ok ? await agentsResponse.json() : []
    }

    setCategories(loadedCategories)
    setPriorities(loadedPriorities)
    setStatuses(loadedStatuses)
    setTickets(loadedTickets)
    setAgents(loadedAgents)
    resetForm(loadedCategories, loadedPriorities, loadedStatuses)
  }

  const resetForm = (
    categoryItems = categories,
    priorityItems = priorities,
    statusItems = statuses,
  ) => {
    setEditingTicketId(null)
    setForm({
      ...emptyForm,
      categoryId: categoryItems[0]?.id ?? '',
      priorityId: priorityItems[1]?.id ?? priorityItems[0]?.id ?? '',
      statusId: statusItems[0]?.id ?? '',
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
      await loadWorkspace(loginData.token, loginData.role)
      setSuccess('Login successful. Ticket workflow is connected to the API.')
    } catch (err) {
      setError(err.message)
      logout(false)
    } finally {
      setIsLoading(false)
    }
  }

  const reloadTickets = async () => {
    const response = await fetch(`${API_BASE_URL}/api/tickets`, { headers: authHeaders() })

    if (!response.ok) {
      throw new Error('Tickets could not be refreshed.')
    }

    const loadedTickets = await response.json()
    setTickets(loadedTickets)

    if (selectedTicket) {
      const updatedSelectedTicket = loadedTickets.find((ticket) => ticket.id === selectedTicket.id)
      setSelectedTicket(updatedSelectedTicket ?? null)
    }
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

      const response = editingTicketId
        ? await fetch(`${API_BASE_URL}/api/tickets/${editingTicketId}`, {
            method: 'PUT',
            headers: { ...authHeaders(), 'Content-Type': 'application/json' },
            body: JSON.stringify({ ...payload, statusId: Number(form.statusId) }),
          })
        : await fetch(`${API_BASE_URL}/api/tickets`, {
            method: 'POST',
            headers: { ...authHeaders(), 'Content-Type': 'application/json' },
            body: JSON.stringify(payload),
          })

      if (!response.ok) {
        throw new Error('Ticket could not be saved.')
      }

      await reloadTickets()
      resetForm()
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
    selectTicket(ticket)
  }

  const deleteTicket = async (ticketId) => {
    if (!window.confirm('Delete this ticket?')) {
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
      if (selectedTicket?.id === ticketId) {
        clearTicketDetails()
      }
      setSuccess('Ticket deleted successfully.')
    } catch (err) {
      setError(err.message)
    } finally {
      setIsLoading(false)
    }
  }

  const selectTicket = async (ticket) => {
    setSelectedTicket(ticket)
    setAssignmentUserId(ticket.assignedToUserAccountId ?? agents[0]?.id ?? '')
    setWorkflowStatusId(ticket.statusId)
    setCommentText('')
    setIsInternal(false)
    setError('')

    try {
      const [commentsResponse, activityResponse] = await Promise.all([
        fetch(`${API_BASE_URL}/api/tickets/${ticket.id}/comments`, { headers: authHeaders() }),
        fetch(`${API_BASE_URL}/api/tickets/${ticket.id}/activity`, { headers: authHeaders() }),
      ])

      if (!commentsResponse.ok || !activityResponse.ok) {
        throw new Error('Ticket history could not be loaded.')
      }

      setComments(await commentsResponse.json())
      setActivity(await activityResponse.json())
    } catch (err) {
      setError(err.message)
      setComments([])
      setActivity([])
    }
  }

  const clearTicketDetails = () => {
    setSelectedTicket(null)
    setComments([])
    setActivity([])
    setCommentText('')
    setIsInternal(false)
    setAssignmentUserId('')
    setWorkflowStatusId('')
  }

  const assignTicket = async () => {
    if (!selectedTicket || !assignmentUserId) {
      return
    }

    setError('')
    setSuccess('')

    try {
      const response = await fetch(`${API_BASE_URL}/api/tickets/${selectedTicket.id}/assign`, {
        method: 'POST',
        headers: { ...authHeaders(), 'Content-Type': 'application/json' },
        body: JSON.stringify({ agentUserId: Number(assignmentUserId) }),
      })

      if (!response.ok) {
        throw new Error('Ticket could not be assigned.')
      }

      const updatedTicket = await response.json()
      await reloadTickets()
      await selectTicket(updatedTicket)
      setSuccess('Ticket assigned successfully.')
    } catch (err) {
      setError(err.message)
    }
  }

  const updateWorkflowStatus = async () => {
    if (!selectedTicket || !workflowStatusId) {
      return
    }

    setError('')
    setSuccess('')

    try {
      const response = await fetch(`${API_BASE_URL}/api/tickets/${selectedTicket.id}/status`, {
        method: 'POST',
        headers: { ...authHeaders(), 'Content-Type': 'application/json' },
        body: JSON.stringify({ statusId: Number(workflowStatusId) }),
      })

      if (!response.ok) {
        throw new Error('Ticket status could not be updated.')
      }

      const updatedTicket = await response.json()
      await reloadTickets()
      await selectTicket(updatedTicket)
      setSuccess('Ticket status updated successfully.')
    } catch (err) {
      setError(err.message)
    }
  }

  const addComment = async () => {
    if (!selectedTicket || !commentText.trim()) {
      return
    }

    setError('')
    setSuccess('')

    try {
      const response = await fetch(`${API_BASE_URL}/api/tickets/${selectedTicket.id}/comments`, {
        method: 'POST',
        headers: { ...authHeaders(), 'Content-Type': 'application/json' },
        body: JSON.stringify({ commentText, isInternal }),
      })

      if (!response.ok) {
        throw new Error('Comment could not be saved.')
      }

      setCommentText('')
      setIsInternal(false)
      await selectTicket(selectedTicket)
      setSuccess(isInternal ? 'Internal note added.' : 'Comment added successfully.')
    } catch (err) {
      setError(err.message)
    }
  }

  const logout = (clearMessage = true) => {
    setToken('')
    setUser(null)
    setTickets([])
    setCategories([])
    setPriorities([])
    setStatuses([])
    setAgents([])
    resetForm([], [], [])
    clearTicketDetails()
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
              {['Dashboard', 'Tickets', 'Workflow', 'History', 'Profile'].map((item) => (
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
                <p className="text-sm font-medium text-slate-500">Assignment and workflow</p>
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

            {error && <Alert tone="error">{error}</Alert>}
            {success && <Alert tone="success">{success}</Alert>}

            <div className="grid gap-4 sm:grid-cols-4">
              <SummaryCard label="Total tickets" value={tickets.length} />
              <SummaryCard label="Open" value={countByStatus('Open')} />
              <SummaryCard label="In progress" value={countByStatus('In Progress')} />
              <SummaryCard label="Resolved" value={countByStatus('Resolved')} />
            </div>

            <section className="mt-6 grid gap-6 xl:grid-cols-[360px_1fr]">
              <form className="rounded-md border border-slate-200 bg-white p-5" onSubmit={saveTicket}>
                <h3 className="text-base font-semibold text-slate-950">
                  {editingTicketId ? 'Edit ticket' : 'Create ticket'}
                </h3>
                <p className="mt-1 text-sm text-slate-500">
                  Ticket records are stored in SQL Server through MVC API controllers.
                </p>

                <TextField
                  label="Title"
                  onChange={(value) => setForm({ ...form, title: value })}
                  value={form.title}
                />
                <TextArea
                  label="Description"
                  onChange={(value) => setForm({ ...form, description: value })}
                  value={form.description}
                />
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
                {editingTicketId && (
                  <SelectField
                    label="Status"
                    onChange={(value) => setForm({ ...form, statusId: value })}
                    options={statuses}
                    value={form.statusId}
                  />
                )}

                <div className="mt-5 flex gap-3">
                  <button
                    className="rounded-md bg-[#0f6b6e] px-4 py-2 text-sm font-bold text-white hover:bg-[#0b575a]"
                    disabled={isLoading}
                    type="submit"
                  >
                    {editingTicketId ? 'Update' : 'Create'}
                  </button>
                  {editingTicketId && (
                    <button
                      className="rounded-md border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
                      onClick={() => resetForm()}
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
                    Select a ticket to manage assignment, comments, and history.
                  </p>
                </div>
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[980px] text-left text-sm">
                    <thead className="bg-slate-50 text-slate-500">
                      <tr>
                        <th className="px-5 py-3 font-semibold">Ref</th>
                        <th className="px-5 py-3 font-semibold">Title</th>
                        <th className="px-5 py-3 font-semibold">Category</th>
                        <th className="px-5 py-3 font-semibold">Priority</th>
                        <th className="px-5 py-3 font-semibold">Status</th>
                        <th className="px-5 py-3 font-semibold">Assigned</th>
                        <th className="px-5 py-3 font-semibold">Actions</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {tickets.map((ticket) => (
                        <tr className={selectedTicket?.id === ticket.id ? 'bg-[#eefafa]' : ''} key={ticket.id}>
                          <td className="px-5 py-4 font-medium text-slate-950">{ticket.ticketNumber}</td>
                          <td className="px-5 py-4">
                            <p className="font-medium text-slate-950">{ticket.title}</p>
                            <p className="mt-1 max-w-sm truncate text-xs text-slate-500">
                              {ticket.description}
                            </p>
                          </td>
                          <td className="px-5 py-4">{ticket.categoryName}</td>
                          <td className="px-5 py-4">{ticket.priorityName}</td>
                          <td className="px-5 py-4">{ticket.statusName}</td>
                          <td className="px-5 py-4">{ticket.assignedAgentName ?? 'Unassigned'}</td>
                          <td className="px-5 py-4">
                            <div className="flex flex-wrap gap-2">
                              <button className="table-action" onClick={() => selectTicket(ticket)} type="button">
                                View
                              </button>
                              <button className="table-action" onClick={() => editTicket(ticket)} type="button">
                                Edit
                              </button>
                              <button className="table-action-danger" onClick={() => deleteTicket(ticket.id)} type="button">
                                Delete
                              </button>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </section>
            </section>

            {selectedTicket && (
              <section className="mt-6 grid gap-6 xl:grid-cols-3">
                <article className="rounded-md border border-slate-200 bg-white p-5">
                  <h3 className="text-base font-semibold text-slate-950">
                    {selectedTicket.ticketNumber} workflow
                  </h3>
                  <p className="mt-1 text-sm text-slate-500">{selectedTicket.title}</p>

                  {canManageWorkflow(user.role) && (
                    <div className="mt-5">
                      <SelectField
                        label="Assign to agent"
                        onChange={setAssignmentUserId}
                        options={agents.map((agent) => ({ id: agent.id, name: agent.fullName }))}
                        value={assignmentUserId}
                      />
                      <button className="mt-3 workflow-button" onClick={assignTicket} type="button">
                        Assign ticket
                      </button>
                    </div>
                  )}

                  <div className="mt-5">
                    <SelectField
                      label="Status"
                      onChange={setWorkflowStatusId}
                      options={statuses}
                      value={workflowStatusId}
                    />
                    <button className="mt-3 workflow-button" onClick={updateWorkflowStatus} type="button">
                      Update status
                    </button>
                  </div>
                </article>

                <article className="rounded-md border border-slate-200 bg-white p-5">
                  <h3 className="text-base font-semibold text-slate-950">Comments and notes</h3>
                  <textarea
                    className="mt-4 min-h-24 w-full resize-y rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0f6b6e] focus:ring-2 focus:ring-[#0f6b6e]/15"
                    onChange={(event) => setCommentText(event.target.value)}
                    placeholder="Write a reply or note"
                    value={commentText}
                  />
                  {canManageWorkflow(user.role) && (
                    <label className="mt-3 flex items-center gap-2 text-sm text-slate-700">
                      <input
                        checked={isInternal}
                        onChange={(event) => setIsInternal(event.target.checked)}
                        type="checkbox"
                      />
                      Internal note
                    </label>
                  )}
                  <button className="mt-4 workflow-button" onClick={addComment} type="button">
                    Add comment
                  </button>

                  <div className="mt-5 space-y-3">
                    {comments.map((comment) => (
                      <div className="rounded-md border border-slate-200 bg-slate-50 p-3" key={comment.id}>
                        <div className="flex items-center justify-between gap-3">
                          <p className="text-sm font-semibold text-slate-950">{comment.authorName}</p>
                          {comment.isInternal && (
                            <span className="rounded bg-amber-100 px-2 py-1 text-xs font-semibold text-amber-700">
                              Internal
                            </span>
                          )}
                        </div>
                        <p className="mt-2 text-sm text-slate-600">{comment.commentText}</p>
                      </div>
                    ))}
                  </div>
                </article>

                <article className="rounded-md border border-slate-200 bg-white p-5">
                  <h3 className="text-base font-semibold text-slate-950">Activity history</h3>
                  <div className="mt-4 space-y-3">
                    {activity.map((item) => (
                      <div className="border-l-2 border-[#0f6b6e] pl-3" key={item.id}>
                        <p className="text-sm font-semibold text-slate-950">{item.actionName}</p>
                        <p className="text-sm text-slate-600">{item.actionDetails}</p>
                        <p className="mt-1 text-xs text-slate-500">By {item.actorName}</p>
                      </div>
                    ))}
                  </div>
                </article>
              </section>
            )}
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
            Ticket assignment, workflow tracking, comments, internal notes, and audit history.
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

          <TextField label="Email address" onChange={setEmail} type="email" value={email} />
          <TextField label="Password" onChange={setPassword} type="password" value={password} />

          {error && <Alert tone="error">{error}</Alert>}

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

function Alert({ children, tone }) {
  const color =
    tone === 'success'
      ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
      : 'border-rose-200 bg-rose-50 text-rose-700'

  return <div className={`mb-5 rounded-md border px-4 py-3 text-sm ${color}`}>{children}</div>
}

function SummaryCard({ label, value }) {
  return (
    <article className="rounded-md border border-slate-200 bg-white p-5">
      <p className="text-sm font-medium text-slate-500">{label}</p>
      <p className="mt-2 text-3xl font-bold text-slate-950">{value}</p>
    </article>
  )
}

function TextField({ label, value, onChange, type = 'text' }) {
  return (
    <label className="mt-4 block text-sm font-semibold text-slate-700">
      {label}
      <input
        className="mt-2 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0f6b6e] focus:ring-2 focus:ring-[#0f6b6e]/15"
        onChange={(event) => onChange(event.target.value)}
        required
        type={type}
        value={value}
      />
    </label>
  )
}

function TextArea({ label, value, onChange }) {
  return (
    <label className="mt-4 block text-sm font-semibold text-slate-700">
      {label}
      <textarea
        className="mt-2 min-h-28 w-full resize-y rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0f6b6e] focus:ring-2 focus:ring-[#0f6b6e]/15"
        onChange={(event) => onChange(event.target.value)}
        required
        value={value}
      />
    </label>
  )
}

function SelectField({ label, options, value, onChange }) {
  return (
    <label className="mt-4 block text-sm font-semibold text-slate-700">
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
