# MAPS API Reference

## Base URL
- **Development:** `http://localhost:5000`
- **Production:** `https://your-domain/api`

## Authentication
All endpoints (except `/api/auth/*` and `/api/health`) require:
```
Authorization: Bearer {access_token}
```

---

## Auth Endpoints

| Method | Route | Access | Description |
|--------|-------|--------|-------------|
| POST | `/api/auth/login` | Public | Login → returns JWT tokens |
| POST | `/api/auth/register` | Public | Register (subject to lock) |
| POST | `/api/auth/refresh` | Public | Refresh access token |
| POST | `/api/auth/revoke` | Auth | Logout / revoke refresh token |
| POST | `/api/auth/approve/{id}` | Admin | Approve pending registration |
| GET  | `/api/auth/me` | Auth | Current user info |

---

## User Management (Admin Only)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/users` | Paginated user list |
| GET | `/api/users/{id}` | User by ID |
| GET | `/api/users/pending` | Pending approvals |
| POST | `/api/users/{id}/approve` | Approve user |
| PUT | `/api/users/{id}/activate` | Activate user |
| PUT | `/api/users/{id}/deactivate` | Deactivate user |
| DELETE | `/api/users/{id}` | Soft-delete user |

---

## Assignments (Admin Only)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/assignments/doctors` | All doctors with patient counts |
| GET | `/api/assignments/unassigned` | Unassigned patients |
| POST | `/api/assignments` | Assign patient to doctor |
| DELETE | `/api/assignments/{id}` | Remove assignment |
| PUT | `/api/assignments/transfer` | Transfer patient to new doctor |

---

## Patients (Doctor / Admin)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/patients/queue` | Doctor's risk-sorted patient queue |
| GET | `/api/patients/{id}/timeline` | Full patient timeline + history |

---

## AI Predictions (Doctor / Admin)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/predictions` | Run new prediction |
| GET | `/api/predictions/{id}` | Prediction by ID |
| GET | `/api/predictions/patient/{id}` | All predictions for patient |
| POST | `/api/predictions/differential` | Generate differential diagnosis |
| POST | `/api/predictions/share` | Share prediction with patient |

---

## Medical Image Analysis (Doctor Only)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/images/analyse` | Upload & analyse image (multipart) |
| GET | `/api/images/{id}` | Image result by ID |
| GET | `/api/images/patient/{id}` | All images for patient |

---

## Risk Assessment (Doctor / Admin)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/risks/patient/{id}` | Latest risk for patient |
| POST | `/api/risks/patient/{id}/recalculate` | Force recalculation |
| GET | `/api/risks/alerts` | High-risk alert list for doctor |

---

## Clinical Notes (Doctor Only)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/notes` | Create note with NLP processing |
| GET | `/api/notes/patient/{id}` | Notes for patient |
| GET | `/api/notes/{id}/entities` | Extracted NLP entities |

---

## Voice Dictation (Doctor Only)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/voice/transcribe` | Transcribe audio → text |
| GET | `/api/voice/health` | Whisper service health check |

---

## AI Chatbot (Doctor Only)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/chatbot/session` | Start new chat session |
| POST | `/api/chatbot/query` | Send query (text/image/voice/doc) |
| GET | `/api/chatbot/history/{sessionId}` | Session message history |
| GET | `/api/chatbot/sessions` | All sessions for doctor |

---

## Medical Literature Search (Doctor Only)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/literature/search` | Semantic search over clinical guidelines |

---

## Drug Interactions (Doctor Only)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/drugs/check-interactions` | Check drug-drug interactions |

---

## Appointments

| Method | Route | Access | Description |
|--------|-------|--------|-------------|
| GET | `/api/appointments` | Patient | My appointments |
| GET | `/api/appointments/slots` | Patient | Available slots |
| POST | `/api/appointments` | Patient | Book appointment |
| PUT | `/api/appointments/{id}/cancel` | Patient | Cancel appointment |

---

## Prescriptions (Doctor / Admin)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/prescriptions/patient/{id}` | Patient's prescriptions |
| POST | `/api/prescriptions` | Create prescription |
| PUT | `/api/prescriptions/{id}/status` | Update status |

---

## Chat (Authenticated)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/chat/history/{userId}` | Message history with user |
| GET | `/api/chat/contacts` | Chat contact list |
| POST | `/api/chat/upload` | Upload file attachment |
| GET | `/api/chat/unread-count` | Unread message count |

---

## Reports (Doctor / Admin)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/reports/patient-summary/{id}` | Generate patient summary PDF |
| POST | `/api/reports/prediction/{id}` | Generate prediction report PDF |
| POST | `/api/reports/consultation/{id}` | Generate consultation report PDF |

---

## Patient (Patient Only)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/patient/dashboard` | Patient dashboard data |
| GET | `/api/patient/health-summary` | Health records & predictions |
| POST | `/api/patient/feedback` | Submit consultation feedback |

---

## Feedback (Admin)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/feedback` | All feedback (paginated) |

---

## Analytics (Admin Only)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/analytics/dashboard` | Full system analytics |
| GET | `/api/analytics/feedback` | Feedback analytics |
| GET | `/api/analytics/audit` | Audit log summary |

---

## System (Admin Only)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/system/containers` | Docker container health |
| GET | `/api/system/health` | API health (public) |
| GET | `/api/system/audit` | Recent audit events |

---

## SignalR Hub

**Endpoint:** `wss://your-domain/api/chat`
**Auth:** Pass JWT via query string `?access_token={token}`

### Client Events (listen)
| Event | Payload | Description |
|-------|---------|-------------|
| `ReceiveMessage` | `MessageDto` | New message received |
| `MessageSent` | `MessageDto` | Sent message confirmed |
| `TypingIndicator` | `TypingIndicatorDto` | Typing status |
| `MessagesRead` | `Guid[]` | Messages marked read |
| `Notification` | `{type, payload}` | Risk alerts, system notifications |

### Server Methods (invoke)
| Method | Parameters | Description |
|--------|-----------|-------------|
| `SendMessage` | `SendMessageRequest` | Send a message |
| `SendTyping` | `receiverId, isTyping` | Broadcast typing status |
| `MarkRead` | `MarkReadRequest` | Mark messages as read |

---

## Standard Response Format

```json
{
  "success": true,
  "message": "Operation successful",
  "data": { },
  "errors": [],
  "timestamp": "2026-05-14T10:00:00Z"
}
```

## Rate Limits

| Endpoint Group | Limit | Window |
|----------------|-------|--------|
| `/api/auth/login` | 10 requests | 15 minutes |
| `/api/auth/register` | 5 requests | 1 hour |
| `/api/predictions` | 50 requests | 1 minute |
| `/api/images` | 20 requests | 1 minute |
| `/api/chatbot` | 30 requests | 1 minute |
| `/api/voice` | 10 requests | 1 minute |
| All others | 200 requests | 1 minute |
