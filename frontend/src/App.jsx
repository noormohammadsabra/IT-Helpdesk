import { useEffect, useMemo, useState } from 'react'
import { HubConnectionBuilder } from '@microsoft/signalr'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import './App.css'

const API_BASE_URL = 'http://127.0.0.1:5090'

const demoAccounts = [
  { role: 'Admin', email: 'admin@ids.com' },
  { role: 'Agent', email: 'agent@ids.com' },
  { role: 'Manager', email: 'manager@ids.com' },
  { role: 'Employee', email: 'employee@ids.com' },
]

const chartColors = ['#0f6b6e', '#2563eb', '#f59e0b', '#dc2626', '#64748b', '#7c3aed']

const canManageWorkflow = (role) => ['Admin', 'Agent', 'Manager'].includes(role)

async function apiRequest(path, token, options = {}) {
  const headers = {
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(options.body instanceof FormData ? {} : { 'Content-Type': 'application/json' }),
    ...options.headers,
  }

  const response = await fetch(`${API_BASE_URL}${path}`, { ...options, headers })

  if (!response.ok) {
    throw new Error(`Request failed: ${response.status}`)
  }

  if (response.status === 204) {
    return null
  }

  return response.json()
}

function App() {
  const queryClient = useQueryClient()
  const [email, setEmail] = useState('employee@ids.com')
  const [password, setPassword] = useState('Password123!')
  const [token, setToken] = useState('')
  const [user, setUser] = useState(null)
  const [selectedTicket, setSelectedTicket] = useState(null)
  const [editingTicket, setEditingTicket] = useState(null)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    formState: { isSubmitting },
  } = useForm()

  const {
    register: registerAttachment,
    handleSubmit: handleAttachmentSubmit,
    reset: resetAttachment,
  } = useForm()

  const isLoggedIn = Boolean(token && user)

  const analyticsQuery = useQuery({
    queryKey: ['analytics', token],
    queryFn: () => apiRequest('/api/dashboard/analytics', token),
    enabled: isLoggedIn,
  })

  const categoriesQuery = useQuery({
    queryKey: ['categories', token],
    queryFn: () => apiRequest('/api/ticket-categories', token),
    enabled: isLoggedIn,
  })

  const prioritiesQuery = useQuery({
    queryKey: ['priorities', token],
    queryFn: () => apiRequest('/api/ticket-priorities', token),
    enabled: isLoggedIn,
  })

  const statusesQuery = useQuery({
    queryKey: ['statuses', token],
    queryFn: () => apiRequest('/api/ticket-statuses', token),
    enabled: isLoggedIn,
  })

  const ticketsQuery = useQuery({
    queryKey: ['tickets', token],
    queryFn: () => apiRequest('/api/tickets', token),
    enabled: isLoggedIn,
  })

  const notificationsQuery = useQuery({
    queryKey: ['notifications', token],
    queryFn: () => apiRequest('/api/notifications', token),
    enabled: isLoggedIn,
  })

  const agentsQuery = useQuery({
    queryKey: ['agents', token],
    queryFn: () => apiRequest('/api/agents', token),
    enabled: isLoggedIn && canManageWorkflow(user?.role),
  })

  const commentsQuery = useQuery({
    queryKey: ['comments', selectedTicket?.id, token],
    queryFn: () => apiRequest(`/api/tickets/${selectedTicket.id}/comments`, token),
    enabled: isLoggedIn && Boolean(selectedTicket),
  })

  const activityQuery = useQuery({
    queryKey: ['activity', selectedTicket?.id, token],
    queryFn: () => apiRequest(`/api/tickets/${selectedTicket.id}/activity`, token),
    enabled: isLoggedIn && Boolean(selectedTicket),
  })

  const attachmentsQuery = useQuery({
    queryKey: ['attachments', selectedTicket?.id, token],
    queryFn: () => apiRequest(`/api/tickets/${selectedTicket.id}/attachments`, token),
    enabled: isLoggedIn && Boolean(selectedTicket),
  })

  useEffect(() => {
    if (!token) {
      return undefined
    }

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/notifications`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .build()

    connection.on('NotificationReceived', () => {
      queryClient.invalidateQueries({ queryKey: ['notifications'] })
      setMessage('New notification received.')
    })

    connection.start().catch(() => {
      setError('Real-time notifications could not connect, but normal API notifications still work.')
    })

    return () => {
      connection.stop()
    }
  }, [queryClient, token])

  useEffect(() => {
    const categories = categoriesQuery.data ?? []
    const priorities = prioritiesQuery.data ?? []
    const statuses = statusesQuery.data ?? []

    if (!editingTicket && categories.length && priorities.length && statuses.length) {
      setValue('categoryId', categories[0].id)
      setValue('priorityId', priorities[1]?.id ?? priorities[0].id)
      setValue('statusId', statuses[0].id)
    }
  }, [categoriesQuery.data, editingTicket, prioritiesQuery.data, setValue, statusesQuery.data])

  const tickets = ticketsQuery.data ?? []
  const analytics = analyticsQuery.data
  const notifications = notificationsQuery.data
  const categories = categoriesQuery.data ?? []
  const priorities = prioritiesQuery.data ?? []
  const statuses = statusesQuery.data ?? []
  const agents = agentsQuery.data ?? []
  const comments = commentsQuery.data ?? []
  const activity = activityQuery.data ?? []
  const attachments = attachmentsQuery.data ?? []

  const unreadCount = useMemo(
    () => (notifications ?? []).filter((notification) => !notification.isRead).length,
    [notifications],
  )

  const login = async (event) => {
    event.preventDefault()
    setError('')
    setMessage('')

    try {
      const loginData = await apiRequest('/api/auth/login', '', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
      })
      setToken(loginData.token)
      setUser(loginData)
      setMessage('Login successful.')
    } catch {
      setError('Invalid email or password.')
    }
  }

  const saveTicketMutation = useMutation({
    mutationFn: (formData) => {
      const payload = {
        title: formData.title,
        description: formData.description,
        categoryId: Number(formData.categoryId),
        priorityId: Number(formData.priorityId),
      }

      if (editingTicket) {
        return apiRequest(`/api/tickets/${editingTicket.id}`, token, {
          method: 'PUT',
          body: JSON.stringify({ ...payload, statusId: Number(formData.statusId) }),
        })
      }

      return apiRequest('/api/tickets', token, {
        method: 'POST',
        body: JSON.stringify(payload),
      })
    },
    onSuccess: () => {
      setMessage(editingTicket ? 'Ticket updated successfully.' : 'Ticket created successfully.')
      clearTicketForm()
      refreshWorkspace()
    },
    onError: () => setError('Ticket could not be saved.'),
  })

  const deleteTicketMutation = useMutation({
    mutationFn: (ticketId) => apiRequest(`/api/tickets/${ticketId}`, token, { method: 'DELETE' }),
    onSuccess: () => {
      setMessage('Ticket deleted successfully.')
      setSelectedTicket(null)
      refreshWorkspace()
    },
    onError: () => setError('Ticket could not be deleted.'),
  })

  const assignTicketMutation = useMutation({
    mutationFn: ({ ticketId, agentUserId }) =>
      apiRequest(`/api/tickets/${ticketId}/assign`, token, {
        method: 'POST',
        body: JSON.stringify({ agentUserId: Number(agentUserId) }),
      }),
    onSuccess: (ticket) => {
      setSelectedTicket(ticket)
      setMessage('Ticket assigned successfully.')
      refreshWorkspace()
    },
    onError: () => setError('Ticket could not be assigned.'),
  })

  const updateStatusMutation = useMutation({
    mutationFn: ({ ticketId, statusId }) =>
      apiRequest(`/api/tickets/${ticketId}/status`, token, {
        method: 'POST',
        body: JSON.stringify({ statusId: Number(statusId) }),
      }),
    onSuccess: (ticket) => {
      setSelectedTicket(ticket)
      setMessage('Status updated successfully.')
      refreshWorkspace()
    },
    onError: () => setError('Status could not be updated.'),
  })

  const addCommentMutation = useMutation({
    mutationFn: (formData) =>
      apiRequest(`/api/tickets/${selectedTicket.id}/comments`, token, {
        method: 'POST',
        body: JSON.stringify({
          commentText: formData.commentText,
          isInternal: Boolean(formData.isInternal),
        }),
      }),
    onSuccess: () => {
      setMessage('Comment saved successfully.')
      reset({ commentText: '', isInternal: false })
      queryClient.invalidateQueries({ queryKey: ['comments', selectedTicket?.id] })
      queryClient.invalidateQueries({ queryKey: ['activity', selectedTicket?.id] })
    },
    onError: () => setError('Comment could not be saved.'),
  })

  const uploadAttachmentMutation = useMutation({
    mutationFn: (formData) => {
      const file = formData.attachment?.[0]
      const uploadData = new FormData()
      uploadData.append('file', file)

      return apiRequest(`/api/tickets/${selectedTicket.id}/attachments`, token, {
        method: 'POST',
        body: uploadData,
      })
    },
    onSuccess: () => {
      setMessage('Attachment uploaded successfully.')
      resetAttachment()
      queryClient.invalidateQueries({ queryKey: ['attachments', selectedTicket?.id] })
      queryClient.invalidateQueries({ queryKey: ['activity', selectedTicket?.id] })
    },
    onError: () => setError('Attachment could not be uploaded.'),
  })

  const markNotificationReadMutation = useMutation({
    mutationFn: (notificationId) =>
      apiRequest(`/api/notifications/${notificationId}/read`, token, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  })

  const refreshWorkspace = () => {
    queryClient.invalidateQueries({ queryKey: ['tickets'] })
    queryClient.invalidateQueries({ queryKey: ['analytics'] })
    queryClient.invalidateQueries({ queryKey: ['notifications'] })
    if (selectedTicket) {
      queryClient.invalidateQueries({ queryKey: ['comments', selectedTicket.id] })
      queryClient.invalidateQueries({ queryKey: ['activity', selectedTicket.id] })
      queryClient.invalidateQueries({ queryKey: ['attachments', selectedTicket.id] })
    }
  }

  const clearTicketForm = () => {
    setEditingTicket(null)
    reset({
      title: '',
      description: '',
      categoryId: categories[0]?.id ?? '',
      priorityId: priorities[1]?.id ?? priorities[0]?.id ?? '',
      statusId: statuses[0]?.id ?? '',
    })
  }

  const startEdit = (ticket) => {
    setEditingTicket(ticket)
    setSelectedTicket(ticket)
    reset({
      title: ticket.title,
      description: ticket.description,
      categoryId: ticket.categoryId,
      priorityId: ticket.priorityId,
      statusId: ticket.statusId,
    })
  }

  const logout = () => {
    setToken('')
    setUser(null)
    setSelectedTicket(null)
    setEditingTicket(null)
    setMessage('')
    setError('')
    queryClient.clear()
  }

  if (!user) {
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
              Analytics dashboard, live notifications, file attachments, and ticket workflow.
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

            <TextInput label="Email address" onChange={setEmail} type="email" value={email} />
            <TextInput label="Password" onChange={setPassword} type="password" value={password} />
            {error && <Alert tone="error">{error}</Alert>}

            <button className="mt-6 w-full rounded-md bg-[#0f6b6e] px-4 py-3 text-sm font-bold text-white hover:bg-[#0b575a]">
              Login
            </button>
          </form>
        </section>
      </main>
    )
  }

  return (
    <main className="min-h-screen bg-[#f6f8fb] text-slate-900">
      <div className="flex min-h-screen">
        <aside className="hidden w-64 border-r border-slate-200 bg-white px-5 py-6 md:block">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">IDS Internal</p>
          <h1 className="mt-1 text-xl font-bold text-slate-950">IT Help Desk</h1>
          <nav className="mt-8 space-y-1 text-sm font-medium">
            {['Dashboard', 'Tickets', 'Attachments', 'Notifications', 'Reports'].map((item) => (
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
              <p className="text-sm font-medium text-slate-500">Analytics and notification center</p>
              <h2 className="text-2xl font-bold text-slate-950">Welcome, {user.fullName}</h2>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <div className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm">
                <span className="font-semibold text-slate-950">{user.role}</span>
                <span className="ml-2 text-slate-500">{user.department}</span>
              </div>
              <div className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm font-semibold">
                {unreadCount} unread
              </div>
              <button className="rounded-md bg-slate-950 px-4 py-2 text-sm font-semibold text-white" onClick={logout}>
                Logout
              </button>
            </div>
          </header>

          {message && <Alert tone="success">{message}</Alert>}
          {error && <Alert tone="error">{error}</Alert>}

          <section className="grid gap-4 sm:grid-cols-5">
            <SummaryCard label="Total" value={analytics?.totalTickets ?? 0} />
            <SummaryCard label="Open" value={analytics?.openTickets ?? 0} />
            <SummaryCard label="In progress" value={analytics?.inProgressTickets ?? 0} />
            <SummaryCard label="Resolved" value={analytics?.resolvedTickets ?? 0} />
            <SummaryCard label="Critical" value={analytics?.criticalTickets ?? 0} />
          </section>

          <section className="mt-6 grid gap-6 xl:grid-cols-3">
            <ChartCard title="Tickets by status">
              <ResponsiveContainer height={220} width="100%">
                <BarChart data={analytics?.ticketsByStatus ?? []}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="name" />
                  <YAxis allowDecimals={false} />
                  <Tooltip />
                  <Bar dataKey="value" fill="#0f6b6e" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </ChartCard>

            <ChartCard title="Tickets by category">
              <ResponsiveContainer height={220} width="100%">
                <PieChart>
                  <Pie data={analytics?.ticketsByCategory ?? []} dataKey="value" nameKey="name" outerRadius={80}>
                    {(analytics?.ticketsByCategory ?? []).map((entry, index) => (
                      <Cell fill={chartColors[index % chartColors.length]} key={entry.name} />
                    ))}
                  </Pie>
                  <Tooltip />
                </PieChart>
              </ResponsiveContainer>
            </ChartCard>

            <ChartCard title="Tickets by priority">
              <ResponsiveContainer height={220} width="100%">
                <BarChart data={analytics?.ticketsByPriority ?? []}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="name" />
                  <YAxis allowDecimals={false} />
                  <Tooltip />
                  <Bar dataKey="value" fill="#2563eb" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </ChartCard>
          </section>

          <section className="mt-6 grid gap-6 xl:grid-cols-[360px_1fr_360px]">
            <TicketForm
              categories={categories}
              editingTicket={editingTicket}
              handleSubmit={handleSubmit}
              isSubmitting={isSubmitting || saveTicketMutation.isPending}
              priorities={priorities}
              register={register}
              resetForm={clearTicketForm}
              saveTicket={saveTicketMutation.mutate}
              statuses={statuses}
            />

            <TicketTable
              deleteTicket={(ticketId) => deleteTicketMutation.mutate(ticketId)}
              editTicket={startEdit}
              selectedTicket={selectedTicket}
              selectTicket={setSelectedTicket}
              tickets={tickets}
            />

            <NotificationCenter
              markRead={(notificationId) => markNotificationReadMutation.mutate(notificationId)}
              notifications={notifications ?? []}
            />
          </section>

          {selectedTicket && (
            <section className="mt-6 grid gap-6 xl:grid-cols-3">
              <WorkflowPanel
                agents={agents}
                assignTicket={(agentUserId) =>
                  assignTicketMutation.mutate({ ticketId: selectedTicket.id, agentUserId })
                }
                canManage={canManageWorkflow(user.role)}
                statuses={statuses}
                ticket={selectedTicket}
                updateStatus={(statusId) =>
                  updateStatusMutation.mutate({ ticketId: selectedTicket.id, statusId })
                }
              />

              <CommentsPanel
                addComment={addCommentMutation.mutate}
                canCreateInternal={canManageWorkflow(user.role)}
                comments={comments}
                register={register}
                handleSubmit={handleSubmit}
              />

              <AttachmentsPanel
                attachments={attachments}
                handleAttachmentSubmit={handleAttachmentSubmit}
                registerAttachment={registerAttachment}
                selectedTicket={selectedTicket}
                token={token}
                uploadAttachment={uploadAttachmentMutation.mutate}
              />
            </section>
          )}

          {selectedTicket && (
            <section className="mt-6 rounded-md border border-slate-200 bg-white p-5">
              <h3 className="text-base font-semibold text-slate-950">Activity history</h3>
              <div className="mt-4 grid gap-3 md:grid-cols-2">
                {activity.map((item) => (
                  <div className="border-l-2 border-[#0f6b6e] pl-3" key={item.id}>
                    <p className="text-sm font-semibold text-slate-950">{item.actionName}</p>
                    <p className="text-sm text-slate-600">{item.actionDetails}</p>
                    <p className="mt-1 text-xs text-slate-500">By {item.actorName}</p>
                  </div>
                ))}
              </div>
            </section>
          )}
        </section>
      </div>
    </main>
  )
}

function TicketForm({
  categories,
  editingTicket,
  handleSubmit,
  isSubmitting,
  priorities,
  register,
  resetForm,
  saveTicket,
  statuses,
}) {
  return (
    <form className="rounded-md border border-slate-200 bg-white p-5" onSubmit={handleSubmit(saveTicket)}>
      <h3 className="text-base font-semibold text-slate-950">{editingTicket ? 'Edit ticket' : 'Create ticket'}</h3>
      <FormInput label="Title" registration={register('title', { required: true })} />
      <FormTextarea label="Description" registration={register('description', { required: true })} />
      <FormSelect label="Category" options={categories} registration={register('categoryId', { required: true })} />
      <FormSelect label="Priority" options={priorities} registration={register('priorityId', { required: true })} />
      {editingTicket && (
        <FormSelect label="Status" options={statuses} registration={register('statusId', { required: true })} />
      )}
      <div className="mt-5 flex gap-3">
        <button className="workflow-button" disabled={isSubmitting} type="submit">
          {editingTicket ? 'Update' : 'Create'}
        </button>
        {editingTicket && (
          <button className="table-action" onClick={resetForm} type="button">
            Cancel
          </button>
        )}
      </div>
    </form>
  )
}

function TicketTable({ tickets, selectedTicket, selectTicket, editTicket, deleteTicket }) {
  return (
    <section className="rounded-md border border-slate-200 bg-white">
      <div className="border-b border-slate-200 px-5 py-4">
        <h3 className="text-base font-semibold text-slate-950">Tickets</h3>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[860px] text-left text-sm">
          <thead className="bg-slate-50 text-slate-500">
            <tr>
              <th className="px-4 py-3">Ref</th>
              <th className="px-4 py-3">Title</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Assigned</th>
              <th className="px-4 py-3">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {tickets.map((ticket) => (
              <tr className={selectedTicket?.id === ticket.id ? 'bg-[#eefafa]' : ''} key={ticket.id}>
                <td className="px-4 py-3 font-semibold">{ticket.ticketNumber}</td>
                <td className="px-4 py-3">{ticket.title}</td>
                <td className="px-4 py-3">{ticket.statusName}</td>
                <td className="px-4 py-3">{ticket.assignedAgentName ?? 'Unassigned'}</td>
                <td className="px-4 py-3">
                  <div className="flex gap-2">
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
  )
}

function NotificationCenter({ notifications, markRead }) {
  return (
    <section className="rounded-md border border-slate-200 bg-white p-5">
      <h3 className="text-base font-semibold text-slate-950">Notification center</h3>
      <div className="mt-4 space-y-3">
        {notifications.map((notification) => (
          <button
            className={`w-full rounded-md border p-3 text-left text-sm ${
              notification.isRead ? 'border-slate-200 bg-white' : 'border-[#0f6b6e] bg-[#eefafa]'
            }`}
            key={notification.id}
            onClick={() => markRead(notification.id)}
            type="button"
          >
            <p className="font-semibold text-slate-950">{notification.title}</p>
            <p className="mt-1 text-slate-600">{notification.message}</p>
          </button>
        ))}
        {notifications.length === 0 && <p className="text-sm text-slate-500">No notifications yet.</p>}
      </div>
    </section>
  )
}

function WorkflowPanel({ agents, assignTicket, canManage, statuses, ticket, updateStatus }) {
  const [agentUserId, setAgentUserId] = useState(ticket.assignedToUserAccountId ?? agents[0]?.id ?? '')
  const [statusId, setStatusId] = useState(ticket.statusId)

  return (
    <section className="rounded-md border border-slate-200 bg-white p-5">
      <h3 className="text-base font-semibold text-slate-950">{ticket.ticketNumber} workflow</h3>
      {canManage && (
        <label className="mt-4 block text-sm font-semibold text-slate-700">
          Assign to
          <select className="form-control" onChange={(event) => setAgentUserId(event.target.value)} value={agentUserId}>
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id}>
                {agent.fullName}
              </option>
            ))}
          </select>
          <button className="mt-3 workflow-button" onClick={() => assignTicket(agentUserId)} type="button">
            Assign
          </button>
        </label>
      )}
      <label className="mt-4 block text-sm font-semibold text-slate-700">
        Status
        <select className="form-control" onChange={(event) => setStatusId(event.target.value)} value={statusId}>
          {statuses.map((status) => (
            <option key={status.id} value={status.id}>
              {status.name}
            </option>
          ))}
        </select>
        <button className="mt-3 workflow-button" onClick={() => updateStatus(statusId)} type="button">
          Update status
        </button>
      </label>
    </section>
  )
}

function CommentsPanel({ addComment, canCreateInternal, comments, handleSubmit, register }) {
  return (
    <section className="rounded-md border border-slate-200 bg-white p-5">
      <h3 className="text-base font-semibold text-slate-950">Comments and notes</h3>
      <form className="mt-4" onSubmit={handleSubmit(addComment)}>
        <textarea className="form-control min-h-24" placeholder="Write a reply or note" {...register('commentText')} />
        {canCreateInternal && (
          <label className="mt-3 flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" {...register('isInternal')} />
            Internal note
          </label>
        )}
        <button className="mt-3 workflow-button" type="submit">
          Add comment
        </button>
      </form>
      <div className="mt-5 space-y-3">
        {comments.map((comment) => (
          <article className="rounded-md border border-slate-200 bg-slate-50 p-3" key={comment.id}>
            <p className="font-semibold">{comment.authorName}</p>
            <p className="mt-1 text-sm text-slate-600">{comment.commentText}</p>
            {comment.isInternal && <p className="mt-1 text-xs font-bold text-amber-700">Internal note</p>}
          </article>
        ))}
      </div>
    </section>
  )
}

function AttachmentsPanel({ attachments, handleAttachmentSubmit, registerAttachment, selectedTicket, token, uploadAttachment }) {
  return (
    <section className="rounded-md border border-slate-200 bg-white p-5">
      <h3 className="text-base font-semibold text-slate-950">File attachments</h3>
      <form className="mt-4" onSubmit={handleAttachmentSubmit(uploadAttachment)}>
        <input className="form-control" type="file" {...registerAttachment('attachment', { required: true })} />
        <button className="mt-3 workflow-button" type="submit">
          Upload file
        </button>
      </form>
      <div className="mt-5 space-y-3">
        {attachments.map((attachment) => (
          <a
            className="block rounded-md border border-slate-200 bg-slate-50 p-3 text-sm font-semibold text-slate-800 hover:bg-slate-100"
            href={`${API_BASE_URL}/api/tickets/${selectedTicket.id}/attachments/${attachment.id}/download?access_token=${token}`}
            key={attachment.id}
            rel="noreferrer"
            target="_blank"
          >
            {attachment.fileName}
            <span className="ml-2 text-xs font-normal text-slate-500">
              {(attachment.fileSizeBytes / 1024).toFixed(1)} KB
            </span>
          </a>
        ))}
      </div>
    </section>
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

function ChartCard({ children, title }) {
  return (
    <article className="rounded-md border border-slate-200 bg-white p-5">
      <h3 className="mb-4 text-base font-semibold text-slate-950">{title}</h3>
      {children}
    </article>
  )
}

function TextInput({ label, value, onChange, type = 'text' }) {
  return (
    <label className="mt-4 block text-sm font-semibold text-slate-700">
      {label}
      <input className="form-control" onChange={(event) => onChange(event.target.value)} required type={type} value={value} />
    </label>
  )
}

function FormInput({ label, registration }) {
  return (
    <label className="mt-4 block text-sm font-semibold text-slate-700">
      {label}
      <input className="form-control" required {...registration} />
    </label>
  )
}

function FormTextarea({ label, registration }) {
  return (
    <label className="mt-4 block text-sm font-semibold text-slate-700">
      {label}
      <textarea className="form-control min-h-24" required {...registration} />
    </label>
  )
}

function FormSelect({ label, options, registration }) {
  return (
    <label className="mt-4 block text-sm font-semibold text-slate-700">
      {label}
      <select className="form-control" required {...registration}>
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
