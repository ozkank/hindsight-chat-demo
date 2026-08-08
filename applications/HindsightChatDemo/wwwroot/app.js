const messagesEl = document.getElementById("messages");
const form = document.getElementById("chat-form");
const input = document.getElementById("chat-input");
const sendBtn = document.getElementById("send-btn");
const newSessionBtn = document.getElementById("new-session-btn");
const sessionIdDisplay = document.getElementById("session-id-display");
const bankIdDisplay = document.getElementById("bank-id-display");
const adminLink = document.getElementById("admin-link");
const memoryViewerBtn = document.getElementById("memory-viewer-btn");
const memoryList = document.getElementById("memory-list");

const userId = "musteri-" + Math.random().toString(36).slice(2, 8);
let sessionId = crypto.randomUUID();

function renderEmptyState() {
  messagesEl.innerHTML = `
    <div class="empty-state">
      Yeni bir arama başlattın. Müşteri gibi bir şey anlat (ör. "Merhaba, kargom 2 haftadır
      teslim edilmedi"), daha önce anlattığın bir şeyi sor ("geçen hafta aramıştım, sorunumu
      hatırlıyor musunuz?") ya da genel bir değerlendirme iste ("bana nasıl bir öneride
      bulunursunuz?").
    </div>`;
}

function toolCallLabel(toolCall) {
  const argsText = JSON.stringify(toolCall.arguments ?? {});
  if (toolCall.name === "retain") {
    return { cls: "retain", text: `🧠 Hindsight'a yazıldı: ${argsText}` };
  }
  if (toolCall.name === "recall") {
    return { cls: "recall", text: `🧠 Hindsight'tan hatırlandı: ${argsText}` };
  }
  if (toolCall.name === "reflect") {
    return { cls: "reflect", text: `🧠 Hindsight'ta değerlendirildi: ${argsText}` };
  }
  return { cls: "recall", text: `🧠 ${toolCall.name}: ${argsText}` };
}

function appendMessage(role, text, toolCalls) {
  if (messagesEl.querySelector(".empty-state")) {
    messagesEl.innerHTML = "";
  }

  const wrapper = document.createElement("div");
  wrapper.className = `message ${role}`;

  const bubble = document.createElement("div");
  bubble.className = "bubble";
  bubble.textContent = text;
  wrapper.appendChild(bubble);

  if (toolCalls && toolCalls.length > 0) {
    const toolCallsEl = document.createElement("div");
    toolCallsEl.className = "tool-calls";
    for (const toolCall of toolCalls) {
      const { cls, text: label } = toolCallLabel(toolCall);
      const tag = document.createElement("div");
      tag.className = `tool-call-tag ${cls}`;
      tag.textContent = label;
      toolCallsEl.appendChild(tag);
    }
    wrapper.appendChild(toolCallsEl);
  }

  messagesEl.appendChild(wrapper);
  messagesEl.scrollTop = messagesEl.scrollHeight;
}

function autoResize() {
  input.style.height = "auto";
  input.style.height = Math.min(input.scrollHeight, 140) + "px";
}

async function loadConfig() {
  try {
    const res = await fetch("/api/config");
    const config = await res.json();
    bankIdDisplay.textContent = config.bankId;
    adminLink.href = config.adminUiUrl;
  } catch {
    bankIdDisplay.textContent = "bilinmiyor";
  }
}

// Fetches memories straight from Hindsight's REST API (see /api/memories in
// Endpoints/MemoryEndpoints.cs) -- no MCP, no LLM. The chat above WRITES through the
// agent's MCP tool calls; this button READS the same bank directly over HTTP, to show
// both integration styles side by side.
async function loadMemories() {
  memoryViewerBtn.disabled = true;
  memoryViewerBtn.textContent = "⏳ okunuyor...";

  try {
    const res = await fetch("/api/memories?limit=8");
    if (!res.ok) throw new Error(res.statusText);
    const data = await res.json();

    memoryList.innerHTML = "";
    if (data.items.length === 0) {
      memoryList.innerHTML = `<div class="memory-empty">Bankada henüz kayıt yok.</div>`;
    }
    for (const item of data.items) {
      const el = document.createElement("div");
      el.className = "memory-item";
      const date = item.date ? new Date(item.date).toLocaleString("tr-TR") : "";
      el.innerHTML = `<span class="memory-text"></span>${item.fact_type ?? ""} · ${date}`;
      el.querySelector(".memory-text").textContent = item.text;
      memoryList.appendChild(el);
    }
  } catch (e) {
    memoryList.innerHTML = `<div class="memory-empty">⚠️ Okunamadı: ${e.message}</div>`;
  } finally {
    memoryViewerBtn.disabled = false;
    memoryViewerBtn.textContent = "📖 Hafızayı REST'ten oku";
  }
}

function startNewSession() {
  sessionId = crypto.randomUUID();
  sessionIdDisplay.textContent = sessionId.slice(0, 8);
  renderEmptyState();
  input.value = "";
  autoResize();
  input.focus();
}

async function sendMessage(message) {
  appendMessage("user", message, null);
  sendBtn.disabled = true;

  try {
    const res = await fetch("/api/chat", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ message, userId, sessionId }),
    });

    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      appendMessage("assistant", `⚠️ Hata: ${err.error ?? res.statusText}`, null);
      return;
    }

    const data = await res.json();
    sessionId = data.sessionId || sessionId;
    sessionIdDisplay.textContent = sessionId.slice(0, 8);
    appendMessage("assistant", data.message, data.toolCalls);
  } catch (e) {
    appendMessage("assistant", `⚠️ Bağlantı hatası: ${e.message}`, null);
  } finally {
    sendBtn.disabled = false;
  }
}

form.addEventListener("submit", (e) => {
  e.preventDefault();
  const message = input.value.trim();
  if (!message) return;
  input.value = "";
  autoResize();
  sendMessage(message);
});

input.addEventListener("input", autoResize);
input.addEventListener("keydown", (e) => {
  if (e.key === "Enter" && !e.shiftKey) {
    e.preventDefault();
    form.requestSubmit();
  }
});

newSessionBtn.addEventListener("click", startNewSession);
memoryViewerBtn.addEventListener("click", loadMemories);

sessionIdDisplay.textContent = sessionId.slice(0, 8);
renderEmptyState();
loadConfig();
