'use strict';

// تسجيل الـ Service Worker + إعادة تحميل تلقائية عند تفعيل نسخة جديدة
// إلغاء تسجيل أي SW قديم — إعادة تحميل مرة واحدة لضمان خلو الصفحة منه
if ('serviceWorker' in navigator) {
  navigator.serviceWorker.getRegistrations().then(regs => {
    if (regs.length > 0) {
      Promise.all(regs.map(r => r.unregister())).then(() => {
        if (!sessionStorage.getItem('sw_gone')) {
          sessionStorage.setItem('sw_gone', '1');
          window.location.reload();
        }
      });
    }
  });
}

/* ════════════════════════════════════════════════════════════════
   State
════════════════════════════════════════════════════════════════ */
const S = {
  token:       localStorage.getItem('token'),
  user:        JSON.parse(localStorage.getItem('user') || 'null'),
  permissions: JSON.parse(localStorage.getItem('permissions') || 'null'),
  page:    'dashboard',
  bookings: { data: [], page: 1, total: 0, status: '', type: '', search: '' },
  members:  { data: [], filter: 'all', search: '' },
  complaints: { data: [], status: '' },
  users:    { data: [], role: '', search: '', status: '' },
};

// تطبيق كلاس المدير فوراً عند تحميل الصفحة (قبل أي شيء)
if (S.user?.role === 'Admin') document.body.classList.add('is-admin');

/* ════════════════════════════════════════════════════════════════
   API
════════════════════════════════════════════════════════════════ */
const API = {
  base: '/api',
  async req(method, path, body) {
    const h = { 'Content-Type': 'application/json' };
    if (S.token) h['Authorization'] = 'Bearer ' + S.token;
    let r;
    try {
      r = await fetch(this.base + path, { method, headers: h, body: body ? JSON.stringify(body) : undefined });
    } catch {
      throw new Error('تعذّر الاتصال بالخادم، تحقق من الإنترنت');
    }
    if (r.status === 204) return null;
    if (r.status === 401) { logout(); throw Object.assign(new Error('SESSION'), { isSession: true }); }
    if (r.status === 403) throw new Error('ليس لديك صلاحية للقيام بهذا الإجراء');
    if (r.status === 429) throw new Error('تجاوزت الحد المسموح من الطلبات، حاول بعد قليل');
    const data = await r.json().catch(() => ({}));
    if (!r.ok) throw new Error(data.message || `خطأ في الخادم (${r.status})`);
    return data;
  },
  get:    (p)    => API.req('GET', p),
  post:   (p, b) => API.req('POST', p, b),
  put:    (p, b) => API.req('PUT', p, b),
  patch:  (p, b) => API.req('PATCH', p, b),
  delete: (p)    => API.req('DELETE', p),
};

/* ════════════════════════════════════════════════════════════════
   Toast
════════════════════════════════════════════════════════════════ */
function handleErr(err) { if (!err?.isSession) toast(err.message, 'error'); }

function toast(msg, type = 'success') {
  const icons = { success: '✅', error: '❌', info: 'ℹ️', warning: '⚠️' };
  const el = document.createElement('div');
  el.className = `toast toast-${type}`;
  el.innerHTML = `<span class="toast-icon">${icons[type]}</span><span>${msg}</span>`;
  document.getElementById('toastContainer').prepend(el);
  setTimeout(() => { el.classList.add('removing'); setTimeout(() => el.remove(), 300); }, 3500);
}

/* ════════════════════════════════════════════════════════════════
   JWT Helpers
════════════════════════════════════════════════════════════════ */
function extractUserId(token) {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    // .NET maps ClaimTypes.NameIdentifier to "nameid" or keeps full URI
    const val = payload['nameid']
      || payload['sub']
      || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']
      || Object.entries(payload).find(([k]) => k.toLowerCase().includes('nameidentifier'))?.[1]
      || '0';
    return parseInt(val, 10) || 0;
  } catch { return 0; }
}

/* ════════════════════════════════════════════════════════════════
   Auth
════════════════════════════════════════════════════════════════ */
document.getElementById('loginForm').addEventListener('submit', async e => {
  e.preventDefault();
  const btn    = document.getElementById('loginBtn');
  const errEl  = document.getElementById('loginError');
  const email  = document.getElementById('loginEmail').value.trim();
  const pass   = document.getElementById('loginPassword').value;
  errEl.classList.add('hidden');
  btn.querySelector('.btn-text').classList.add('hidden');
  btn.querySelector('.btn-loader').classList.remove('hidden');
  btn.disabled = true;
  try {
    const data = await API.post('/auth/login', { email, password: pass });
    S.token = data.token;
    S.user  = { id: extractUserId(data.token), fullName: data.fullName, email: data.email, role: data.role };
    S.permissions = data.permissions;
    localStorage.setItem('token', S.token);
    localStorage.setItem('user', JSON.stringify(S.user));
    localStorage.setItem('permissions', JSON.stringify(S.permissions));
    bootApp();
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove('hidden');
  } finally {
    btn.querySelector('.btn-text').classList.remove('hidden');
    btn.querySelector('.btn-loader').classList.add('hidden');
    btn.disabled = false;
  }
});

function togglePass(id) {
  const inp = document.getElementById(id);
  inp.type = inp.type === 'password' ? 'text' : 'password';
}

function switchAuthTab(tab) {
  const isLogin = tab === 'login';
  document.getElementById('tabLogin').classList.toggle('active', isLogin);
  document.getElementById('tabRegister').classList.toggle('active', !isLogin);
  document.getElementById('loginForm').classList.toggle('hidden', !isLogin);
  document.getElementById('registerForm').classList.toggle('hidden', isLogin);
  document.getElementById('loginError').classList.add('hidden');
  document.getElementById('registerError').classList.add('hidden');
}

document.getElementById('registerForm').addEventListener('submit', async e => {
  e.preventDefault();
  const btn    = document.getElementById('registerBtn');
  const errEl  = document.getElementById('registerError');
  const name   = document.getElementById('regFullName').value.trim();
  const phone  = document.getElementById('regPhone').value.trim();
  const email  = document.getElementById('regEmail').value.trim();
  const pass   = document.getElementById('regPassword').value;
  const confirm = document.getElementById('regConfirm').value;

  errEl.classList.add('hidden');

  if (!name)  { errEl.textContent = 'أدخل الاسم الكامل'; errEl.classList.remove('hidden'); return; }
  if (!phone) { errEl.textContent = 'أدخل رقم الجوال';   errEl.classList.remove('hidden'); return; }
  if (!email) { errEl.textContent = 'أدخل البريد الإلكتروني'; errEl.classList.remove('hidden'); return; }
  if (pass.length < 6) { errEl.textContent = 'كلمة المرور يجب أن تكون 6 أحرف على الأقل'; errEl.classList.remove('hidden'); return; }
  if (pass !== confirm) { errEl.textContent = 'كلمة المرور وتأكيدها غير متطابقتين'; errEl.classList.remove('hidden'); return; }

  btn.querySelector('.btn-text').classList.add('hidden');
  btn.querySelector('.btn-loader').classList.remove('hidden');
  btn.disabled = true;

  try {
    const data = await API.post('/auth/register', { fullName: name, phoneNumber: phone, email, password: pass });
    S.token = data.token;
    S.user  = { id: extractUserId(data.token), fullName: data.fullName, email: data.email, role: data.role };
    S.permissions = data.permissions;
    localStorage.setItem('token', S.token);
    localStorage.setItem('user', JSON.stringify(S.user));
    localStorage.setItem('permissions', JSON.stringify(S.permissions));
    bootApp();
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove('hidden');
  } finally {
    btn.querySelector('.btn-text').classList.remove('hidden');
    btn.querySelector('.btn-loader').classList.add('hidden');
    btn.disabled = false;
  }
});

function logout() {
  localStorage.removeItem('token');
  localStorage.removeItem('user');
  localStorage.removeItem('permissions');
  localStorage.removeItem('dashCache');
  S.token = null; S.user = null; S.permissions = null; S._dashPrefetch = null;
  document.getElementById('appShell').classList.add('hidden');
  document.getElementById('loginScreen').classList.remove('hidden');
  document.getElementById('loginPassword').value = '';
}

/* ════════════════════════════════════════════════════════════════
   Boot
════════════════════════════════════════════════════════════════ */
function bootApp() {
  const name     = S.user?.fullName || 'مستخدم';
  const initials = name.charAt(0);
  const admin    = isAdmin();

  // CSS class يتحكم في العناصر الخاصة بالمدير فوراً (قبل الإظهار)
  document.body.classList.toggle('is-admin', admin);

  document.getElementById('userName').textContent       = name;
  document.getElementById('userAvatar').textContent     = initials;
  document.getElementById('headerUserName').textContent = name;
  document.getElementById('headerAvatar').textContent   = initials;

  // شارة الدور — ذهبي للمدير، رمادي للمستخدم
  const roleEl = document.getElementById('userRole');
  if (roleEl) {
    roleEl.textContent = admin ? 'مدير النظام' : 'مستخدم';
    roleEl.className   = 'user-role ' + (admin ? 'role-admin' : 'role-user');
  }

  applyNavPermissions();

  // الرئيسية ثابتة دائماً عند التحميل
  // فعّل page-dashboard في DOM قبل الإظهار
  document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
  document.getElementById('page-dashboard').classList.add('active');
  document.querySelectorAll('.nav-item').forEach(n => {
    n.classList.toggle('active', n.dataset.page === 'dashboard');
  });
  document.getElementById('headerTitle').textContent = 'الرئيسية';

  // أظهر التطبيق — الرئيسية جاهزة مسبقاً
  document.getElementById('loginScreen').classList.add('hidden');
  document.getElementById('appShell').classList.remove('hidden');

  loadDashboard();
}

/* ════════════════════════════════════════════════════════════════
   Permissions Helper
════════════════════════════════════════════════════════════════ */
function isAdmin() { return S.user?.role === 'Admin'; }

function can(key) {
  if (isAdmin()) return true;
  if (!S.permissions) return false;
  // camelCase key → match object property
  const map = {
    viewBookings:    'viewBookings',    createBookings:  'createBookings',
    confirmBookings: 'confirmBookings', deleteBookings:  'deleteBookings',
    viewMembers:     'viewMembers',     manageMembers:   'manageMembers',
    viewAllComplaints:'viewAllComplaints', respondComplaints:'respondComplaints',
    viewDashboard:   'viewDashboard',   viewReports:     'viewReports',
  };
  return !!S.permissions[map[key] ?? key];
}

function applyNavPermissions() {
  const admin = isAdmin();

  // أزرار تعتمد على الصلاحيات
  const btnAddMember  = document.getElementById('btnAddMember');
  if (btnAddMember)  btnAddMember.style.display  = admin ? '' : 'none';

  const btnAddBooking = document.getElementById('addBookingBtn');
  if (btnAddBooking) btnAddBooking.style.display = (admin || can('createBookings')) ? '' : 'none';

  // لوحة الحجوزات القادمة في الرئيسية
  const upcomingPanel = document.getElementById('upcomingPanel');
  if (upcomingPanel) upcomingPanel.style.display = (admin || can('viewBookings')) ? '' : 'none';

  if (admin) return; // المدير يرى كل شيء

  const rules = {
    dashboard:  'viewDashboard',
    bookings:   'viewBookings',
    members:    'viewMembers',
    complaints: false, // مرئي دائماً
    users:      null,  // مخفي دائماً للمستخدم العادي
  };

  document.querySelectorAll('.nav-item[data-page]').forEach(el => {
    const page = el.dataset.page;
    const perm = rules[page];
    el.style.display = (perm === null || (perm && !can(perm))) ? 'none' : '';
  });

  // إخفاء عنوان القسم إذا كانت جميع عناصره مخفية
  document.querySelectorAll('.nav-section-label').forEach(label => {
    let visible = false;
    let sib = label.nextElementSibling;
    while (sib && !sib.classList.contains('nav-section-label')) {
      if (sib.classList.contains('nav-item') && sib.style.display !== 'none') {
        visible = true; break;
      }
      sib = sib.nextElementSibling;
    }
    label.style.display = visible ? '' : 'none';
  });
}

/* ════════════════════════════════════════════════════════════════
   Router
════════════════════════════════════════════════════════════════ */
const PAGE_TITLES = {
  dashboard:  'الرئيسية',
  bookings:   'الحجوزات',
  members:    'أعضاء الجمعية',
  complaints: 'الشكاوى والمقترحات',
  users:      'المستخدمون والصلاحيات',
};

function navigate(page) {
  // حماية — تحقق من الصلاحية قبل الانتقال
  const pagePerms = { dashboard:'viewDashboard', bookings:'viewBookings',
                      members:'viewMembers',
                      complaints: false,  users: null };
  const perm = pagePerms[page];
  if (perm === null && !isAdmin())      { toast('هذه الصفحة للمدير فقط', 'error'); return; }
  if (perm && !can(perm))               { toast('ليس لديك صلاحية لهذه الصفحة', 'error'); return; }

  if (page !== 'dashboard') _carouselStopAll();
  S.page = page;
  document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
  document.getElementById('page-' + page).classList.add('active');
  document.querySelectorAll('.nav-item').forEach(n => {
    n.classList.toggle('active', n.dataset.page === page);
  });
  document.getElementById('headerTitle').textContent = PAGE_TITLES[page] || page;
  if (window.innerWidth < 768) closeSidebar();

  const loaders = { dashboard: loadDashboard, bookings: loadBookings, members: loadMembers, complaints: loadComplaints, users: loadUsers };
  loaders[page]?.();
}

function toggleSidebar() {
  const sb = document.getElementById('sidebar');
  const ov = document.getElementById('sidebarOverlay');
  sb.classList.toggle('open');
  ov.classList.toggle('active');
}
function closeSidebar() {
  document.getElementById('sidebar').classList.remove('open');
  document.getElementById('sidebarOverlay').classList.remove('active');
}

/* ════════════════════════════════════════════════════════════════
   Modal helpers
════════════════════════════════════════════════════════════════ */
function openModal(id)  { document.getElementById(id).classList.remove('hidden'); }
function closeModal(id) { document.getElementById(id).classList.add('hidden'); }

window.addEventListener('keydown', e => {
  if (e.key === 'Escape') document.querySelectorAll('.modal-overlay:not(.hidden)').forEach(m => m.classList.add('hidden'));
});
document.querySelectorAll('.modal-overlay').forEach(o => {
  o.addEventListener('click', e => { if (e.target === o) o.classList.add('hidden'); });
});

function confirm(title, msg, onOk) {
  document.getElementById('confirmTitle').textContent = title;
  document.getElementById('confirmMsg').textContent   = msg;
  const btn = document.getElementById('confirmOkBtn');
  const clone = btn.cloneNode(true);
  btn.parentNode.replaceChild(clone, btn);
  clone.addEventListener('click', () => { closeModal('confirmModal'); onOk(); });
  openModal('confirmModal');
}

/* ════════════════════════════════════════════════════════════════
   Helpers
════════════════════════════════════════════════════════════════ */
function fmtDate(d)  { return d ? new Date(d).toLocaleDateString('ar-SA') : '-'; }
function fmtMoney(n) { return Number(n || 0).toLocaleString('ar-OM') + ' ر.ع'; }

const typeLabel = { Wedding: '💍 زواج', Condolence: '🕊 عزاء', General: '🎉 عام' };
const statusLabel = { Pending: 'قيد الانتظار', Confirmed: 'مؤكد', Cancelled: 'ملغي', Completed: 'مكتمل' };
const statusClass = { Pending: 'badge-pending', Confirmed: 'badge-confirmed', Cancelled: 'badge-cancelled', Completed: 'badge-completed' };
const typeClass   = { Wedding: 'badge-wedding', Condolence: 'badge-condolence', General: 'badge-general' };
const monthNames  = ['','يناير','فبراير','مارس','أبريل','مايو','يونيو','يوليو','أغسطس','سبتمبر','أكتوبر','نوفمبر','ديسمبر'];

function badge(cls, label) { return `<span class="badge ${cls}">${label}</span>`; }

function countUp(el, target, duration = 900) {
  const start = Date.now();
  const update = () => {
    const p = Math.min(1, (Date.now() - start) / duration);
    el.textContent = Math.floor(p * target).toLocaleString('ar-SA');
    if (p < 1) requestAnimationFrame(update);
    else el.textContent = target.toLocaleString('ar-SA');
  };
  requestAnimationFrame(update);
}

/* ════════════════════════════════════════════════════════════════
   DASHBOARD
════════════════════════════════════════════════════════════════ */
function _applyDashData(d) {
  if (!d?.stats) return;
  renderStats(d.stats);
  renderUpcoming(d.upcoming ?? []);
  updateBadges(d.stats);
  renderEventsData(d.events ?? []);
  renderPublicBoard(d.publicBoard ?? []);
  if (can('viewAllComplaints')) renderRecentComplaints(d.recentComplaints ?? []);
}

const DASH_CACHE_TTL_MS = 3 * 60 * 1000; // 3 دقائق — لا يُرسل طلب للسيرفر إذا الكاش أحدث من ذلك

function invalidateDashCache() {
  localStorage.removeItem('dashCache');
  S._dashPrefetch = null;
}

async function loadDashboard() {
  initDashHero();
  applyNavPermissions();

  const adminCard = document.getElementById('adminComplaintsCard');
  if (adminCard) adminCard.style.display = can('viewAllComplaints') ? '' : 'none';
  const addEventBtn = document.getElementById('addEventBtn');
  if (addEventBtn) addEventBtn.classList.toggle('hidden', S.user?.role !== 'Admin');

  // ① محاولة تحميل الكاش المحلي فوراً
  let cacheAge = Infinity;
  const raw = localStorage.getItem('dashCache');
  if (raw) {
    try {
      const cached = JSON.parse(raw);
      // دعم الصيغة القديمة (بدون ts) والجديدة (مع ts)
      const data = cached.ts ? cached.data : cached;
      const ts   = cached.ts || 0;
      cacheAge   = Date.now() - ts;
      if (data?.stats) _applyDashData(data);
    } catch { localStorage.removeItem('dashCache'); }
  }

  // ② إذا الكاش طازج (< 3 دقائق) — لا حاجة لطلب سيرفر
  if (cacheAge < DASH_CACHE_TTL_MS) {
    S._dashPrefetch = null;
    return;
  }

  // ③ الكاش قديم أو غير موجود — استخدم prefetch إن وُجد وإلا ابدأ طلباً جديداً
  const fetchPromise = S._dashPrefetch || API.get('/dashboard/full');
  const membersPromise = can('viewMembers') ? API.get('/members').catch(() => []) : Promise.resolve([]);
  S._dashPrefetch = null;

  try {
    const [d, members] = await Promise.all([fetchPromise, membersPromise]);
    if (!d?.stats) return;
    const mList = members || [];
    d.stats.memberTotalPaid    = mList.reduce((s, m) => s + (m.paidAmount    || 0), 0);
    d.stats.memberTotalOverdue = mList.reduce((s, m) => s + Math.max(0, (m.totalPaymentDue || 0) - (m.paidAmount || 0)), 0);
    const lean = { ...d, events: [] };
    try { localStorage.setItem('dashCache', JSON.stringify({ data: lean, ts: Date.now() })); } catch { }
    _applyDashData(d);
  } catch (err) {
    if (!raw) handleErr(err);
  }

  // حمّل الأحداث دائماً بشكل منفصل (صورها محفوظة في DB وليست في الكاش)
  loadEvents();
}

let _dashClockTimer = null;
function initDashHero() {
  const greetEl  = document.getElementById('dashGreeting');
  const nameEl   = document.getElementById('dashHeroName');
  const dateEl   = document.getElementById('dashHeroDate');
  const dayEl    = document.getElementById('dashHeroDay');
  const timeEl   = document.getElementById('dashHeroTime');
  if (!greetEl) return;

  const hour = new Date().getHours();
  const greeting = hour < 5 ? 'مساء النور' : hour < 12 ? 'صباح الخير' : hour < 17 ? 'مساء الخير' : 'مساء النور';
  greetEl.textContent = greeting + '،';
  nameEl.textContent  = S.user?.fullName || 'المدير';

  const now = new Date();
  dateEl.textContent = now.toLocaleDateString('ar-SA', { year:'numeric', month:'long', day:'numeric' });
  dayEl.textContent  = now.toLocaleDateString('ar-SA', { weekday:'long' });

  const tick = () => {
    const t = new Date();
    const hh = String(t.getHours()).padStart(2,'0');
    const mm = String(t.getMinutes()).padStart(2,'0');
    if (timeEl) timeEl.textContent = `${hh}:${mm}`;
  };
  tick();
  if (_dashClockTimer) clearInterval(_dashClockTimer);
  _dashClockTimer = setInterval(tick, 10000);
}

/* ════════════════════════════════════════════════════════════════
   EVENTS (آخر الأحداث)
════════════════════════════════════════════════════════════════ */
async function loadEvents() {
  const el = document.getElementById('eventsList');
  if (!el) return;
  el.innerHTML = '<div class="events-loading">جارٍ التحميل...</div>';
  try {
    const data = await API.get('/events');
    renderEvents(Array.isArray(data) ? data : []);
  } catch { el.innerHTML = '<div class="events-loading">تعذّر تحميل الأحداث</div>'; }
}

function renderEventsData(list) {
  renderEvents(Array.isArray(list) ? list : []);
}

function renderPublicBoard(list) {
  const el = document.getElementById('publicBoard');
  if (!el) return;
  if (!list || !list.length) {
    el.innerHTML = '<div class="loading-text">لا توجد إعلانات حالياً</div>';
    return;
  }
  el.innerHTML = list.map(c => {
    const typeClass = c.type === 'Suggestion' ? 'badge-suggestion' : 'badge-complaint';
    const typeLabel = c.type === 'Suggestion' ? '💡 اقتراح' : '🚨 شكوى';
    return `
    <div class="public-complaint-card">
      <div class="public-complaint-header">
        <div style="display:flex;align-items:center;gap:8px">
          ${badge(typeClass, typeLabel)}
          <strong>${escHtml(c.title)}</strong>
        </div>
        <span class="public-complaint-date">${fmtDate(c.createdAt)}</span>
      </div>
      <p class="public-complaint-body">${escHtml(c.content)}</p>
      ${c.adminResponse ? `<div class="public-complaint-response">💬 ${escHtml(c.adminResponse)}</div>` : ''}
      <div class="public-complaint-meta">👤 ${c.isAnonymous ? 'مجهول' : escHtml(c.senderName || 'مجهول')}</div>
    </div>`;
  }).join('');
}

/* ── Events data map (for edit lookup) ───────────────── */
const _eventsMap = new Map(); // eventId → event object

/* ── Carousel engine ─────────────────────────────────── */
const _carousels = new Map(); // eventId → { idx, timer }

function _carouselGo(evId, idx) {
  const state = _carousels.get(evId);
  if (!state) return;
  const slides = document.querySelectorAll(`#ev-slides-${evId} .event-slide`);
  const dots   = document.querySelectorAll(`#ev-slides-${evId} .event-dot`);
  const n = slides.length;
  idx = ((idx % n) + n) % n;
  slides.forEach((s, i) => s.classList.toggle('active', i === idx));
  dots.forEach((d, i)   => d.classList.toggle('active', i === idx));
  state.idx = idx;
}

function _carouselStart(evId, total) {
  if (total <= 1 || _carousels.has(evId)) return;
  const timer = setInterval(() => {
    const s = _carousels.get(evId);
    if (s) _carouselGo(evId, s.idx + 1);
  }, 3800);
  _carousels.set(evId, { idx: 0, timer });
}

function _carouselStopAll() {
  _carousels.forEach(s => clearInterval(s.timer));
  _carousels.clear();
}
/* ─────────────────────────────────────────────────────── */

function renderEvents(list) {
  const el = document.getElementById('eventsList');
  if (!el) return;
  _carouselStopAll();
  _eventsMap.clear();
  list.forEach(ev => _eventsMap.set(ev.id, ev));
  if (!list.length) {
    el.innerHTML = '<div class="events-empty"><div class="events-empty-icon">📭</div><p>لا توجد أحداث منشورة بعد</p></div>';
    return;
  }
  const isAdmin = S.user?.role === 'Admin';
  el.innerHTML = list.map(ev => {
    const media = Array.isArray(ev.media) && ev.media.length > 0
      ? ev.media
      : (ev.mediaUrl && ev.mediaType !== 'none' ? [{ mediaUrl: ev.mediaUrl, mediaType: ev.mediaType }] : []);

    let slideshowHtml = '';
    if (media.length > 0) {
      const slides = media.map((m, i) => {
        const url   = escHtml(m.mediaUrl || m.MediaUrl || '');
        const type  = m.mediaType || m.MediaType || 'image';
        const isVid = type === 'video';
        return `<div class="event-slide${i === 0 ? ' active' : ''}" onclick="openMediaViewer('${type}','${url}')">
          ${isVid
            ? `<video src="${url}" preload="metadata"></video><span class="event-slide-video-badge">▶ فيديو</span>`
            : `<img src="${url}" loading="lazy" />`}
          <div class="event-slide-overlay">${isVid ? '▶ تشغيل' : '🔍 تكبير'}</div>
        </div>`;
      }).join('');

      const dots = media.length > 1
        ? `<div class="event-dots">${media.map((_, i) =>
            `<button class="event-dot${i === 0 ? ' active' : ''}" onclick="event.stopPropagation();_carouselGo(${ev.id},${i})"></button>`
          ).join('')}</div>` : '';

      const arrows = media.length > 1
        ? `<button class="event-arrow prev" onclick="event.stopPropagation();_carouselGo(${ev.id},(_carousels.get(${ev.id})?.idx??0)+1)">&#8250;</button>
           <button class="event-arrow next" onclick="event.stopPropagation();_carouselGo(${ev.id},(_carousels.get(${ev.id})?.idx??0)-1)">&#8249;</button>`
        : '';

      slideshowHtml = `<div class="event-slideshow" id="ev-slides-${ev.id}">${slides}${dots}${arrows}</div>`;
    }

    return `
    <div class="event-card">
      <div class="event-body">
        <div class="event-header">
          <h4 class="event-title">${escHtml(ev.title)}</h4>
          ${isAdmin ? `<div style="display:flex;gap:4px">
            <button class="btn-icon small" title="تعديل" onclick="openEditEventModal(_eventsMap.get(${ev.id}))">✏️</button>
            <button class="btn-icon delete small" title="حذف" onclick="deleteEvent(${ev.id})">🗑️</button>
          </div>` : ''}
        </div>
        <div class="event-meta">
          <span>👤 ${escHtml(ev.createdBy)}</span>
          <span>🕐 ${fmtDate(ev.createdAt)}</span>
        </div>
        ${ev.description ? `<p class="event-desc">${escHtml(ev.description)}</p>` : ''}
      </div>
      ${slideshowHtml}
    </div>`;
  }).join('');

  // ابدأ الـ carousel بعد رسم الـ DOM
  list.forEach(ev => {
    const cnt = (Array.isArray(ev.media) && ev.media.length > 0)
      ? ev.media.length
      : (ev.mediaUrl && ev.mediaType !== 'none' ? 1 : 0);
    _carouselStart(ev.id, cnt);
  });
}

// حالة مودال الأحداث
let _eventFiles   = [];   // ملفات جديدة للرفع
let _editingEvId  = null; // null = إنشاء، رقم = تعديل
let _deleteMediaIds = [];  // IDs الوسائط المحذوفة عند التعديل

function openEventModal() {
  _resetEventModal();
  document.getElementById('eventModalTitle').textContent = 'إضافة حدث جديد';
  document.getElementById('eventSubmitBtn').textContent  = '📢 نشر الحدث';
  openModal('eventModal');
}

function openEditEventModal(ev) {
  _resetEventModal();
  _editingEvId = ev.id;
  document.getElementById('eventModalTitle').textContent = 'تعديل الحدث';
  document.getElementById('eventSubmitBtn').textContent  = '💾 حفظ التعديلات';
  document.getElementById('eventTitle').value = ev.title || '';
  document.getElementById('eventDesc').value  = ev.description || '';

  // عرض الوسائط الحالية
  const media = Array.isArray(ev.media) && ev.media.length > 0 ? ev.media
    : (ev.mediaUrl && ev.mediaType !== 'none' ? [{ id: 0, mediaUrl: ev.mediaUrl, mediaType: ev.mediaType }] : []);

  const grp = document.getElementById('eventCurrentMediaGroup');
  const cur = document.getElementById('eventCurrentMedia');
  if (media.length > 0) {
    cur.innerHTML = media.map(m => {
      const url   = m.mediaUrl || '';
      const isVid = (m.mediaType || '') === 'video';
      return `<div class="event-upload-thumb" id="cur-media-${m.id}">
        ${isVid ? `<video src="${escHtml(url)}" preload="metadata"></video>` : `<img src="${escHtml(url)}" />`}
        <button class="event-upload-thumb-remove" onclick="_markDeleteMedia(${m.id})" title="حذف">✕</button>
        <div class="event-upload-thumb-label">${isVid ? '📹 فيديو' : '🖼️ صورة'}</div>
      </div>`;
    }).join('');
    grp.classList.remove('hidden');
  } else {
    grp.classList.add('hidden');
  }

  openModal('eventModal');
}

function _resetEventModal() {
  _editingEvId    = null;
  _eventFiles     = [];
  _deleteMediaIds = [];
  document.getElementById('eventTitle').value = '';
  document.getElementById('eventDesc').value  = '';
  document.getElementById('eventMedia').value = '';
  document.getElementById('eventCurrentMediaGroup').classList.add('hidden');
  document.getElementById('eventCurrentMedia').innerHTML = '';
  document.getElementById('eventUploadGrid').innerHTML   = '';
  document.getElementById('eventSubmitBtn').disabled = false;
}

function _markDeleteMedia(mediaId) {
  if (mediaId === 0) return; // backward-compat single media, skip
  _deleteMediaIds.push(mediaId);
  const el = document.getElementById(`cur-media-${mediaId}`);
  if (el) el.remove();
}

// ضغط الصورة على المتصفح قبل الرفع (max 1200px، جودة 82%)
function _compressImage(file) {
  return new Promise(resolve => {
    if (!file.type.startsWith('image/')) { resolve(file); return; }
    const reader = new FileReader();
    reader.onload = e => {
      const img = new Image();
      img.onload = () => {
        const MAX = 1200;
        let w = img.width, h = img.height;
        if (w > MAX || h > MAX) {
          if (w >= h) { h = Math.round(h * MAX / w); w = MAX; }
          else        { w = Math.round(w * MAX / h); h = MAX; }
        }
        const canvas = document.createElement('canvas');
        canvas.width = w; canvas.height = h;
        canvas.getContext('2d').drawImage(img, 0, 0, w, h);
        canvas.toBlob(blob => {
          resolve(new File([blob], file.name.replace(/\.\w+$/, '.jpg'),
            { type: 'image/jpeg', lastModified: Date.now() }));
        }, 'image/jpeg', 0.82);
      };
      img.src = e.target.result;
    };
    reader.readAsDataURL(file);
  });
}

function addEventMedia() {
  const input = document.getElementById('eventMedia');
  for (const file of input.files) _eventFiles.push(file);
  input.value = '';
  _renderEventUploadGrid();
}

function _renderEventUploadGrid() {
  const grid = document.getElementById('eventUploadGrid');
  if (!grid) return;
  if (!_eventFiles.length) { grid.innerHTML = ''; return; }
  grid.innerHTML = _eventFiles.map((f, i) => {
    const url   = URL.createObjectURL(f);
    const isVid = f.type.startsWith('video/');
    return `<div class="event-upload-thumb">
      ${isVid ? `<video src="${url}" preload="metadata"></video>` : `<img src="${url}" />`}
      <button class="event-upload-thumb-remove" onclick="_removeEventFile(${i})" title="حذف">✕</button>
      <div class="event-upload-thumb-label">${escHtml(f.name)}</div>
    </div>`;
  }).join('');
}

function _removeEventFile(idx) {
  _eventFiles.splice(idx, 1);
  _renderEventUploadGrid();
}

async function submitEvent() {
  const title = document.getElementById('eventTitle').value.trim();
  if (!title) { toast('أدخل اسم الحدث', 'error'); return; }
  const desc = document.getElementById('eventDesc').value.trim();

  // منع النشر المزدوج + إظهار حالة التحميل
  const btn      = document.getElementById('eventSubmitBtn');
  const origText = btn.textContent;
  btn.disabled    = true;
  btn.textContent = '⏳ جارٍ الرفع...';

  // ضغط ثم تحويل إلى base64
  btn.textContent = '🗜 جارٍ الضغط...';
  const compressed = await Promise.all(_eventFiles.map(_compressImage));
  btn.textContent = '⏳ جارٍ الرفع...';

  const mediaBase64 = await Promise.all(compressed.map(f => new Promise(resolve => {
    const reader = new FileReader();
    reader.onload = e => resolve(e.target.result); // data URL جاهز
    reader.readAsDataURL(f);
  })));

  try {
    const h = { 'Authorization': 'Bearer ' + S.token, 'Content-Type': 'application/json' };
    let r;
    if (_editingEvId) {
      r = await fetch(`/api/events/${_editingEvId}`, {
        method: 'PUT', headers: h,
        body: JSON.stringify({ title, description: desc || null,
          deleteMediaIds: _deleteMediaIds, media: mediaBase64 })
      });
    } else {
      r = await fetch('/api/events', {
        method: 'POST', headers: h,
        body: JSON.stringify({ title, description: desc || null, media: mediaBase64 })
      });
    }
    const data = await r.json().catch(() => ({}));
    if (!r.ok) throw new Error(data.message || 'خطأ في الخادم');
    toast(_editingEvId ? 'تم تحديث الحدث' : 'تم نشر الحدث بنجاح', 'success');
    closeModal('eventModal');
    invalidateDashCache();
    loadEvents();
  } catch (err) {
    toast(err.message, 'error');
    btn.disabled    = false;
    btn.textContent = origText;
  }
}

function deleteEvent(id) {
  confirm('حذف الحدث', 'هل تريد حذف هذا الحدث؟', async () => {
    try {
      await API.delete('/events/' + id);
      toast('تم حذف الحدث', 'success');
      invalidateDashCache();
      loadEvents();
    } catch (err) { handleErr(err); }
  });
}

function openMediaViewer(type, url) {
  const content = document.getElementById('mediaViewerContent');
  content.innerHTML = type === 'video'
    ? `<video src="${url}" controls autoplay class="media-viewer-media"></video>`
    : `<img src="${url}" class="media-viewer-media" />`;
  openModal('mediaViewerModal');
}

async function loadPublicBoard() {
  const el = document.getElementById('publicBoard');
  if (!el) return;
  try {
    const data = await API.get('/complaints/public');
    const list = Array.isArray(data) ? data : [];
    if (!list.length) {
      el.innerHTML = '<div class="loading-text">لا توجد إعلانات حالياً</div>';
      return;
    }
    el.innerHTML = list.map(c => {
      const typeClass = c.type === 'Suggestion' ? 'badge-suggestion' : 'badge-complaint';
      const typeLabel = c.type === 'Suggestion' ? '💡 اقتراح' : '🚨 شكوى';
      return `
      <div class="public-complaint-card">
        <div class="public-complaint-header">
          <div style="display:flex;align-items:center;gap:8px">
            ${badge(typeClass, typeLabel)}
            <strong>${escHtml(c.title)}</strong>
          </div>
          <span class="public-complaint-date">${fmtDate(c.createdAt)}</span>
        </div>
        <p class="public-complaint-body">${escHtml(c.content)}</p>
        ${c.adminResponse ? `<div class="public-complaint-response">💬 ${escHtml(c.adminResponse)}</div>` : ''}
        <div class="public-complaint-meta">👤 ${c.isAnonymous ? 'مجهول' : escHtml(c.senderName || 'مجهول')}</div>
      </div>`;
    }).join('');
  } catch { if (el) el.innerHTML = '<div class="loading-text">تعذّر تحميل الإعلانات</div>'; }
}

function escHtml(str) {
  return (str || '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function openNewComplaintModal() {
  document.getElementById('ncTitle').value   = '';
  document.getElementById('ncContent').value = '';
  document.getElementById('ncAnonymous').checked = false;
  openModal('newComplaintModal');
}

async function submitNewComplaint() {
  const type    = document.querySelector('input[name="ncType"]:checked')?.value || 'Complaint';
  const title   = document.getElementById('ncTitle').value.trim();
  const content = document.getElementById('ncContent').value.trim();
  const isAnonymous = document.getElementById('ncAnonymous').checked;
  if (!title)   { toast('أدخل عنواناً', 'error'); return; }
  if (!content) { toast('أدخل التفاصيل', 'error'); return; }
  try {
    await API.post('/complaints', { type, title, content, isAnonymous });
    const label = type === 'Suggestion' ? 'اقتراحك' : 'شكواك';
    toast(`تم إرسال ${label} بنجاح، سيراجعه المدير قريباً`, 'success');
    closeModal('newComplaintModal');
    refreshBadges();
    if (S.page === 'complaints') loadComplaints();
  } catch (err) { handleErr(err); }
}

function renderStats(s) {
  const grid = document.getElementById('statsGrid');
  const allCards = [
    { perm:'viewBookings', icon: '📅', label: 'الحجوزات هذا الشهر', value: s.thisMonthBookings, sub: `${s.pendingBookings} قيد الانتظار`, color: '#2563eb', bg: '#eff6ff' },
    { perm:'viewMembers',  icon: '💰', label: 'إجمالي المدفوع',      money: s.memberTotalPaid,    sub: `من اشتراكات الأعضاء`, color: '#059669', bg: '#ecfdf5' },
    { perm:'viewMembers',  icon: '⏳', label: 'إجمالي المتأخرات',    money: s.memberTotalOverdue, sub: `متأخرات الأعضاء`,      color: '#dc2626', bg: '#fef2f2' },
    { perm:'viewMembers',  icon: '👥', label: 'الأعضاء النشطون',     value: s.activeMembers,      sub: `الإجمالي: ${s.totalMembers}`,      color: '#7c3aed', bg: '#f5f3ff' },
  ];
  const cards = isAdmin() ? allCards : allCards.filter(c => can(c.perm));
  if (!cards.length) { grid.innerHTML = ''; return; }
  grid.innerHTML = cards.map(c => {
    const isMoney = c.money != null;
    const displayed = isMoney ? fmtMoney(c.money) : (c.value ?? 0).toLocaleString('ar-SA');
    return `
    <div class="stat-card" style="--stat-color:${c.color};--stat-bg:${c.bg}">
      <div class="stat-card-row">
        <div class="stat-text">
          <div class="stat-label">${c.label}</div>
          <div class="stat-value" data-val="${c.value ?? c.money}" data-money="${isMoney}">${displayed}</div>
          <div class="stat-sub">${c.sub}</div>
        </div>
        <div class="stat-icon-bubble">${c.icon}</div>
      </div>
      <div class="stat-bottom-bar"></div>
    </div>`;
  }).join('');

  grid.querySelectorAll('[data-money="false"]').forEach(el => {
    countUp(el, parseInt(el.dataset.val) || 0);
  });
}

function renderUpcoming(list) {
  const el = document.getElementById('upcomingList');
  if (!list.length) {
    el.innerHTML = '<div class="dash-empty"><div class="dash-empty-icon">📭</div><p>لا توجد حجوزات قادمة</p></div>';
    return;
  }
  const avatarColors = ['#2563eb','#059669','#c9a84c','#7c3aed','#dc2626','#0891b2'];
  el.innerHTML = list.map((b, i) => `
    <div class="upcoming-row">
      <div class="upcoming-av" style="background:${avatarColors[i % avatarColors.length]}">${(b.guestName||'؟')[0]}</div>
      <div class="upcoming-info">
        <div class="upcoming-name">${escHtml(b.guestName)}</div>
        <div class="upcoming-phone">${b.phoneNumber}</div>
      </div>
      <div class="upcoming-right">
        <div class="upcoming-date">${fmtDate(b.startDate)}</div>
        ${badge(statusClass[b.status]||'', statusLabel[b.status]||b.status)}
      </div>
    </div>`).join('');
}

function renderRecentComplaints(list) {
  const el = document.getElementById('recentComplaints');
  if (!list.length) {
    el.innerHTML = '<div class="dash-empty"><div class="dash-empty-icon">✅</div><p>لا توجد شكاوى جديدة</p></div>';
    return;
  }
  el.innerHTML = list.map(c => {
    const isSugg = c.type === 'Suggestion';
    const stCls  = c.status === 'New' ? 'badge-new' : c.status === 'Resolved' ? 'badge-resolved' : 'badge-review';
    const stLbl  = c.status === 'New' ? 'جديد' : c.status === 'Resolved' ? 'تم الحل' : 'قيد المراجعة';
    return `
    <div class="complaint-row">
      <div class="complaint-row-top">
        <div class="complaint-row-badges">
          ${badge(isSugg ? 'badge-suggestion' : 'badge-complaint', isSugg ? '💡 اقتراح' : '🚨 شكوى')}
          ${badge(stCls, stLbl)}
        </div>
        <span class="complaint-row-date">${fmtDate(c.createdAt)}</span>
      </div>
      <div class="complaint-row-title">${escHtml(c.title)}</div>
      <div class="complaint-row-meta">👤 ${c.isAnonymous ? 'مجهول' : escHtml(c.senderName || 'مجهول')}</div>
    </div>`;
  }).join('');
}

function updateBadges(s) {
  const pb = document.getElementById('pendingBadge');
  if (pb) { if (s.pendingBookings > 0) { pb.textContent = s.pendingBookings; pb.style.display = ''; } else { pb.style.display = 'none'; } }
  const cb = document.getElementById('complaintsBadge');
  if (cb) { if (s.newComplaints > 0) { cb.textContent = s.newComplaints; cb.style.display = ''; } else { cb.style.display = 'none'; } }
}

async function refreshBadges() {
  if (!S.token) return;
  try {
    const s = await API.get('/dashboard/badges');
    updateBadges(s);
  } catch { /* صامت */ }
}

// تحديث دوري كل 30 ثانية
setInterval(refreshBadges, 30_000);

/* ════════════════════════════════════════════════════════════════
   BOOKINGS
════════════════════════════════════════════════════════════════ */
async function loadBookings() {
  const params = new URLSearchParams({ page: S.bookings.page, pageSize: 15 });
  if (S.bookings.status) params.set('status', S.bookings.status);
  if (S.bookings.type)   params.set('type',   S.bookings.type);
  document.getElementById('bookingsTable').innerHTML = '<div class="loading-center">جارٍ التحميل...</div>';
  try {
    const data = await API.get('/bookings?' + params);
    S.bookings.data  = data.items;
    S.bookings.total = data.total;
    renderBookingsTable();
    renderPagination('bookingsPagination', data.total, 15, S.bookings.page, p => { S.bookings.page = p; loadBookings(); });
  } catch (err) { handleErr(err); }
}

function filterBookings() {
  S.bookings.search = document.getElementById('bookingSearch').value.toLowerCase();
  S.bookings.type   = document.getElementById('bookingType').value;
  S.bookings.page   = 1;
  loadBookings();
}

document.getElementById('bookingStatusTabs').addEventListener('click', e => {
  const tab = e.target.closest('.tab');
  if (!tab) return;
  document.querySelectorAll('#bookingStatusTabs .tab').forEach(t => t.classList.remove('active'));
  tab.classList.add('active');
  S.bookings.status = tab.dataset.status;
  S.bookings.page   = 1;
  loadBookings();
});

function renderBookingsTable() {
  const el = document.getElementById('bookingsTable');
  let rows = S.bookings.data;
  if (S.bookings.search) rows = rows.filter(b => b.guestName.toLowerCase().includes(S.bookings.search) || b.phoneNumber.includes(S.bookings.search));
  if (!rows.length) { el.innerHTML = '<div class="empty-state"><div class="empty-icon">📭</div><p>لا توجد حجوزات</p></div>'; return; }

  const isAdmin = S.user?.role === 'Admin';
  el.innerHTML = `<table>
    <thead><tr>
      <th>#</th><th>صاحب المناسبة</th><th>التاريخ</th><th>المدة</th>
      <th>النوع</th><th>الحالة</th><th>التكلفة</th>
      <th>إجراءات</th>
    </tr></thead>
    <tbody>${rows.map((b, i) => `
      <tr>
        <td style="color:var(--text-3);font-size:12px">${((S.bookings.page-1)*15)+i+1}</td>
        <td>
          <strong>${b.guestName}</strong>
          <div class="td-sub">${b.phoneNumber}</div>
        </td>
        <td>${fmtDate(b.startDate)}<div class="td-sub">→ ${fmtDate(b.endDate)}</div></td>
        <td>${b.durationDays} يوم</td>
        <td>${badge(typeClass[b.type] || '', typeLabel[b.type] || b.type)}</td>
        <td>${badge(statusClass[b.status] || '', statusLabel[b.status] || b.status)}</td>
        <td>${fmtMoney(b.cost)}</td>
        <td>
          <div class="actions">
            <button class="btn-icon view" title="التفاصيل" onclick="viewBooking(${b.id})">👁</button>
            ${isAdmin && b.status === 'Pending' ? `
              <button class="btn-icon check" title="تأكيد" onclick="quickStatus(${b.id},'Confirmed')">✓</button>
              <button class="btn-icon reject" title="رفض" onclick="quickStatus(${b.id},'Cancelled')">✗</button>` : ''}
            ${b.status === 'Pending' ? `<button class="btn-icon edit" title="تعديل" onclick="editBooking(${b.id})">✎</button>` : ''}
            ${isAdmin ? `<button class="btn-icon del" title="حذف" onclick="deleteBooking(${b.id},'${b.guestName}')">🗑</button>` : ''}
          </div>
        </td>
      </tr>`).join('')}
    </tbody></table>`;
}

async function quickStatus(id, status) {
  const labels = { Confirmed: 'تأكيد', Cancelled: 'رفض' };
  confirm(`${labels[status]} الحجز`, 'هل أنت متأكد؟', async () => {
    try {
      await API.patch(`/bookings/${id}/admin`, { status });
      invalidateDashCache();
      refreshBadges();
      toast(`تم ${labels[status] === 'تأكيد' ? 'تأكيد' : 'رفض'} الحجز`, 'success');
      loadBookings();
    } catch (err) { handleErr(err); }
  });
}

async function deleteBooking(id, name) {
  confirm('حذف الحجز', `هل تريد حذف حجز "${name}"؟`, async () => {
    try {
      await API.delete(`/bookings/${id}`);
      invalidateDashCache();
      refreshBadges();
      toast('تم حذف الحجز', 'success');
      loadBookings();
    } catch (err) { handleErr(err); }
  });
}

async function viewBooking(id) {
  try {
    const b = await API.get(`/bookings/${id}`);
    const isAdmin = S.user?.role === 'Admin';
    document.getElementById('bookingDetailBody').innerHTML = `
      <div class="detail-grid">
        <div class="detail-item"><label>صاحب المناسبة</label><div class="detail-val">${b.guestName}</div></div>
        <div class="detail-item"><label>رقم الجوال</label><div class="detail-val">${b.phoneNumber}</div></div>
        <div class="detail-item"><label>تاريخ البداية</label><div class="detail-val">${fmtDate(b.startDate)}</div></div>
        <div class="detail-item"><label>تاريخ النهاية</label><div class="detail-val">${fmtDate(b.endDate)}</div></div>
        <div class="detail-item"><label>المدة</label><div class="detail-val">${b.durationDays} يوم</div></div>
        <div class="detail-item"><label>النوع</label><div class="detail-val">${badge(typeClass[b.type]||'', typeLabel[b.type]||b.type)}</div></div>
        <div class="detail-item"><label>الحالة</label><div class="detail-val">${badge(statusClass[b.status]||'', statusLabel[b.status]||b.status)}</div></div>
        <div class="detail-item"><label>التكلفة</label><div class="detail-val">${fmtMoney(b.cost)}</div></div>
      </div>
      ${b.notes ? `<div class="detail-section"><h4>ملاحظات</h4><p style="font-size:13.5px;color:var(--text-2)">${b.notes}</p></div>` : ''}
      ${b.adminNote ? `<div class="detail-section" style="border-right:3px solid var(--warning);padding-right:12px;background:var(--warning-bg)"><h4>ملاحظة الإدارة</h4><p style="font-size:13.5px;color:var(--warning)">${b.adminNote}</p></div>` : ''}
      ${isAdmin ? `
      <div class="detail-section">
        <h4>إجراءات الإدارة</h4>
        <div style="display:flex;gap:8px;flex-wrap:wrap;margin-bottom:10px">
          ${b.status === 'Pending' ? `<button class="btn-primary btn-sm" onclick="quickStatus(${b.id},'Confirmed');closeModal('bookingDetailModal')">✓ تأكيد الحجز</button>` : ''}
          ${b.status === 'Pending' ? `<button class="btn-danger btn-sm" onclick="quickStatus(${b.id},'Cancelled');closeModal('bookingDetailModal')">✗ رفض الحجز</button>` : ''}
          ${b.status === 'Confirmed' ? `<button class="btn-secondary btn-sm" onclick="quickStatus(${b.id},'Completed');closeModal('bookingDetailModal')">☑ تعليم كمكتمل</button>` : ''}
        </div>
        <textarea class="admin-note-input" id="adminNoteInput" rows="2" placeholder="أضف ملاحظة إدارية..." dir="rtl">${b.adminNote || ''}</textarea>
        <button class="btn-secondary btn-sm" style="margin-top:8px" onclick="saveAdminNote(${b.id})">حفظ الملاحظة</button>
      </div>` : ''}`;

    openModal('bookingDetailModal');
  } catch (err) { handleErr(err); }
}

async function saveAdminNote(id) {
  const note = document.getElementById('adminNoteInput').value;
  try {
    await API.patch(`/bookings/${id}/admin`, { adminNote: note });
    toast('تم حفظ الملاحظة', 'success');
  } catch (err) { handleErr(err); }
}

/* ── Booking Modal ── */
function openBookingModal(booking) {
  const form = document.getElementById('bookingForm');
  form.reset();
  document.getElementById('bookingId').value = '';
  document.getElementById('conflictAlert').classList.add('hidden');
  document.getElementById('bookingModalTitle').textContent = 'حجز جديد';
  document.getElementById('bookingSubmitBtn').textContent  = 'حفظ الحجز';
  if (booking) {
    document.getElementById('bookingModalTitle').textContent = 'تعديل الحجز';
    document.getElementById('bookingSubmitBtn').textContent  = 'حفظ التعديل';
    document.getElementById('bookingId').value    = booking.id;
    document.getElementById('bGuestName').value   = booking.guestName;
    document.getElementById('bPhone').value       = booking.phoneNumber;
    document.getElementById('bType').value        = booking.type;
    document.getElementById('bCost').value        = booking.cost;
    document.getElementById('bStart').value       = booking.startDate?.substring(0,10);
    document.getElementById('bEnd').value         = booking.endDate?.substring(0,10);
    document.getElementById('bNotes').value       = booking.notes || '';
  }
  openModal('bookingModal');
}

async function editBooking(id) {
  try {
    const b = await API.get(`/bookings/${id}`);
    openBookingModal(b);
  } catch (err) { handleErr(err); }
}

async function checkConflict() {
  const start = document.getElementById('bStart').value;
  const end   = document.getElementById('bEnd').value;
  const alert = document.getElementById('conflictAlert');
  if (!start || !end || start >= end) { alert.classList.add('hidden'); return; }
  const bookingId = document.getElementById('bookingId').value;
  try {
    const params = new URLSearchParams({ start, end });
    if (bookingId) params.set('excludeId', bookingId);
    const data = await API.get('/bookings/check-conflict?' + params);
    if (data.hasConflict) {
      const c = data.conflicts[0];
      const isPending = c.status === 'Pending';
      alert.className = 'conflict-alert' + (isPending ? ' warning-mode' : '');
      alert.innerHTML = `⚠️ تعارض مع حجز <strong>${c.guestName}</strong> (${fmtDate(c.startDate)} - ${fmtDate(c.endDate)})<br>
        <small>الحالة: ${statusLabel[c.status]} — ${isPending ? 'حجز العزاء سيلغي هذا الحجز تلقائياً' : 'لا يمكن الحجز في هذه الفترة'}</small>`;
      alert.classList.remove('hidden');
    } else { alert.classList.add('hidden'); }
  } catch {}
}

async function submitBooking() {
  const id    = document.getElementById('bookingId').value;
  const start = document.getElementById('bStart').value;
  const end   = document.getElementById('bEnd').value;
  if (!document.getElementById('bGuestName').value.trim()) { toast('أدخل اسم صاحب المناسبة', 'error'); return; }
  if (!start || !end) { toast('أدخل التواريخ', 'error'); return; }
  if (start >= end)   { toast('تاريخ البداية يجب أن يكون قبل تاريخ النهاية', 'error'); return; }

  const body = {
    guestName:   document.getElementById('bGuestName').value.trim(),
    phoneNumber: document.getElementById('bPhone').value.trim(),
    type:        document.getElementById('bType').value,
    cost:        parseFloat(document.getElementById('bCost').value) || 0,
    startDate:   start,
    endDate:     end,
    notes:       document.getElementById('bNotes').value.trim(),
  };

  try {
    if (id) await API.put('/bookings/' + id, body);
    else    await API.post('/bookings', body);
    invalidateDashCache();
    refreshBadges();
    toast(id ? 'تم تحديث الحجز' : 'تم إنشاء الحجز', 'success');
    closeModal('bookingModal');
    loadBookings();
  } catch (err) { handleErr(err); }
}

/* ════════════════════════════════════════════════════════════════
   MEMBERS
════════════════════════════════════════════════════════════════ */
async function loadMembers() {
  document.getElementById('membersTable').innerHTML = '<div class="loading-center">جارٍ التحميل...</div>';
  try {
    const endpoint = S.members.filter === 'delinquent' ? '/members/delinquent' : '/members';
    const data = await API.get(endpoint);
    S.members.data = Array.isArray(data) ? data : [];
    renderMembersTable();
  } catch (err) { handleErr(err); }
}

document.getElementById('memberTabs').addEventListener('click', e => {
  const tab = e.target.closest('.tab');
  if (!tab) return;
  document.querySelectorAll('#memberTabs .tab').forEach(t => t.classList.remove('active'));
  tab.classList.add('active');
  S.members.filter = tab.dataset.filter;
  loadMembers();
});

function filterMembers() {
  S.members.search = document.getElementById('memberSearch').value.toLowerCase();
  renderMembersTable();
}

function renderMembersStats(data) {
  const el = document.getElementById('memberStatsBar');
  if (!el) return;

  let cards;
  if (S.members.filter === 'delinquent') {
    const totalOverdue = data.reduce((s, m) => s + (m.overdueAmount ?? m.totalDebt ?? 0), 0);
    cards = [
      { icon: '⚠️', label: 'إجمالي المتأخرين',  val: data.length,   color: '#dc2626', bg: '#fef2f2' },
      { icon: '💸', label: 'إجمالي المتأخرات',   money: totalOverdue, color: '#b91c1c', bg: '#fff1f2' },
    ];
  } else {
    const total      = data.length;
    const totalPaid  = data.reduce((s, m) => s + (m.paidAmount || 0), 0);
    const delinquent = data.filter(m => m.totalDebt > 0).length;
    const monthly    = data.filter(m => m.status === 'Active').reduce((s, m) => s + (m.monthlySubscription || 0), 0);
    cards = [
      { icon: '👥', label: 'إجمالي الأعضاء',     val: total,      color: '#2563eb', bg: '#eff6ff' },
      { icon: '💵', label: 'إجمالي المدفوع',      money: totalPaid, color: '#059669', bg: '#ecfdf5' },
      { icon: '⚠️', label: 'المتأخرون في الدفع', val: delinquent, color: '#dc2626', bg: '#fef2f2' },
    ];
  }

  el.innerHTML = cards.map(c => `
    <div class="stat-card" style="--stat-color:${c.color};--stat-bg:${c.bg}">
      <div class="stat-card-row">
        <div class="stat-text">
          <div class="stat-label">${c.label}</div>
          <div class="stat-value">${c.money != null ? fmtMoney(c.money) : c.val}</div>
        </div>
        <div class="stat-icon-bubble">${c.icon}</div>
      </div>
      <div class="stat-bottom-bar"></div>
    </div>`).join('');
}

function renderMembersTable() {
  const el = document.getElementById('membersTable');
  let rows = S.members.data;
  if (S.members.search) rows = rows.filter(m =>
    m.fullName.toLowerCase().includes(S.members.search) || m.phoneNumber.includes(S.members.search));

  renderMembersStats(S.members.data);

  if (!rows.length) { el.innerHTML = '<div class="empty-state"><div class="empty-icon">👥</div><p>لا توجد نتائج</p></div>'; return; }

  const canManage  = isAdmin() || can('manageMembers');
  const isDelinquentView = S.members.filter === 'delinquent';

  if (isDelinquentView) rows = [...rows].sort((a, b) => (b.totalDebt || 0) - (a.totalDebt || 0));

  const maxDebtInView = isDelinquentView ? Math.max(...rows.map(m => m.totalDebt || 0), 1) : 1;

  el.innerHTML = `<table class="members-table">
    <thead><tr>
      <th style="width:36px">#</th>
      <th>العضو</th>
      <th>الحالة</th>
      <th>إجمالي الدفع</th>
      <th>المدفوع</th>
      <th>المتأخرات</th>
      <th>نسبة السداد</th>
      <th>آخر تحديث</th>
      <th>إجراءات</th>
    </tr></thead>
    <tbody>${rows.map((m, i) => {
      const overdue    = m.overdueAmount ?? Math.max(0, (m.totalPaymentDue || 0) - (m.paidAmount || 0));
      const hasDebt    = overdue > 0;
      const hasData    = (m.totalPaymentDue || 0) > 0;
      const fullPaid   = hasData && !hasDebt;
      const paidPct    = hasData ? Math.min(100, Math.round(((m.paidAmount || 0) / m.totalPaymentDue) * 100)) : 0;
      const avatarCls  = hasDebt ? 'delinquent' : m.status === 'Active' ? 'active' : 'inactive';

      const rowCls = hasDebt
        ? (overdue >= 1000 ? 'row-danger-high' : 'row-danger')
        : fullPaid ? 'row-success'
        : '';

      const barColor   = paidPct === 100 ? '#059669' : paidPct >= 60 ? '#d97706' : '#dc2626';
      const progressBar = hasData
        ? `<div class="pay-progress">
             <div class="pay-progress-bar">
               <div class="pay-progress-fill" style="width:${paidPct}%;background:${barColor}"></div>
             </div>
             <span class="pay-progress-label" style="color:${barColor}">${paidPct}%</span>
           </div>`
        : `<span style="color:var(--text-3);font-size:12px">—</span>`;

      return `
      <tr class="${rowCls}">
        <td class="col-num">${i + 1}</td>
        <td>
          <div style="display:flex;align-items:center;gap:12px">
            <div class="member-avatar ${avatarCls}">${m.fullName.charAt(0)}</div>
            <div>
              <strong style="font-size:14px">${m.fullName}</strong>
              <div class="td-sub">📱 ${m.phoneNumber}</div>
              ${m.nationalId ? `<div class="td-sub">🪪 ${m.nationalId}</div>` : ''}
            </div>
          </div>
        </td>
        <td>${badge(m.status === 'Active' ? 'badge-active' : 'badge-inactive', m.status === 'Active' ? 'نشط' : 'غير نشط')}</td>
        <td class="col-money">${hasData ? fmtMoney(m.totalPaymentDue) : '<span class="no-data">—</span>'}</td>
        <td class="col-money paid">${hasData ? fmtMoney(m.paidAmount || 0) : '<span class="no-data">—</span>'}</td>
        <td>
          ${hasDebt
            ? `<span class="overdue-badge" onclick="goDelinquent()" title="انتقل لقائمة المتأخرين">
                 ⚠️ ${fmtMoney(overdue)}
               </span>`
            : hasData
              ? `<span class="paid-badge">✓ مسدّد</span>`
              : `<span class="no-data">—</span>`}
        </td>
        <td>${progressBar}</td>
        <td class="col-date">${m.lastPaymentDate ? fmtDate(m.lastPaymentDate) : '<span class="no-data">—</span>'}</td>
        <td>
          <div class="member-actions">
            <button class="mact-btn mact-view" onclick="viewMember(${m.id})">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="14" height="14"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
              <span>ملف</span>
            </button>
            <button class="mact-btn mact-pay" onclick="openMemberPayModal(${m.id},'${m.fullName}')">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="14" height="14"><rect x="2" y="5" width="20" height="14" rx="2"/><path d="M2 10h20"/><path d="M6 15h4"/></svg>
              <span>دفع</span>
            </button>
            ${canManage ? `<button class="mact-btn mact-edit" onclick="editMember(${m.id})">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="14" height="14"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
              <span>تعديل</span>
            </button>` : ''}
            ${isAdmin() ? `<button class="mact-btn mact-del" onclick="deleteMember(${m.id},'${m.fullName}')">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="14" height="14"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/></svg>
              <span>حذف</span>
            </button>` : ''}
          </div>
        </td>
      </tr>`;
    }).join('')}
    </tbody></table>`;
}

async function viewMember(id) {
  const local = S.members.data.find(x => x.id === id);
  if (!local) return;
  let m = { ...local };
  try {
    const detail = await API.get(`/members/${id}`);
    m = { ...m, address: detail.address };
  } catch {}

  const hasDebt   = m.totalDebt > 0;
  const avatarCls = hasDebt ? 'delinquent' : m.status === 'Active' ? 'active' : 'inactive';

  const overdue    = m.overdueAmount ?? Math.max(0, (m.totalPaymentDue || 0) - (m.paidAmount || 0));
  const paidPct    = m.totalPaymentDue > 0 ? Math.min(100, Math.round((m.paidAmount / m.totalPaymentDue) * 100)) : 100;

  document.getElementById('memberDetailBody').innerHTML = `
    <div class="member-profile-header">
      <div class="member-avatar-xl ${avatarCls}">${m.fullName.charAt(0)}</div>
      <div class="member-profile-info">
        <h3>${m.fullName}</h3>
        <div style="display:flex;gap:8px;align-items:center;margin-top:8px;flex-wrap:wrap">
          ${badge(m.status === 'Active' ? 'badge-active' : 'badge-inactive', m.status === 'Active' ? 'نشط' : 'غير نشط')}
          ${hasDebt ? `<span class="badge badge-unpaid" style="cursor:pointer" onclick="closeModal('memberDetailModal');goDelinquent()" title="الذهاب لصفحة المتأخرين">⚠️ متأخرات: ${fmtMoney(overdue)}</span>` : badge('badge-paid', '✓ لا يوجد دين')}
        </div>
      </div>
    </div>

    <div class="member-profile-stats">
      <div class="mstat">
        <div class="mstat-icon" style="background:#eff6ff;color:var(--info)">📱</div>
        <div><div class="mstat-label">رقم الجوال</div><div class="mstat-value">${m.phoneNumber}</div></div>
      </div>
      ${m.nationalId ? `
      <div class="mstat">
        <div class="mstat-icon" style="background:#f5f3ff;color:var(--purple)">🪪</div>
        <div><div class="mstat-label">رقم الهوية</div><div class="mstat-value">${m.nationalId}</div></div>
      </div>` : ''}
      <div class="mstat">
        <div class="mstat-icon" style="background:#fef2f2;color:var(--danger)">💸</div>
        <div><div class="mstat-label">إجمالي مبلغ الدفع</div><div class="mstat-value">${fmtMoney(m.totalPaymentDue || 0)}</div></div>
      </div>
      <div class="mstat">
        <div class="mstat-icon" style="background:#ecfdf5;color:var(--success)">✅</div>
        <div><div class="mstat-label">المبلغ المدفوع</div><div class="mstat-value">${fmtMoney(m.paidAmount || 0)}</div></div>
      </div>
      ${m.lastPaymentDate ? `
      <div class="mstat">
        <div class="mstat-icon" style="background:#fffbeb;color:var(--warning)">📅</div>
        <div><div class="mstat-label">تاريخ آخر تحديث</div><div class="mstat-value">${fmtDate(m.lastPaymentDate)}</div></div>
      </div>` : ''}
      ${m.address ? `
      <div class="mstat">
        <div class="mstat-icon" style="background:#f5f3ff;color:var(--purple)">📍</div>
        <div><div class="mstat-label">العنوان</div><div class="mstat-value">${m.address}</div></div>
      </div>` : ''}
    </div>

    <div class="${hasDebt ? 'member-debt-section' : 'member-debt-section ok'}">
      ${hasDebt ? `
      <div class="member-debt-header">
        <span>💳 وضع الاشتراك</span>
        <span class="debt-amount-big">${fmtMoney(overdue)}</span>
      </div>
      <div class="debt-detail">
        <span>الإجمالي المطلوب: <strong>${fmtMoney(m.totalPaymentDue || 0)}</strong></span>
        <span>المدفوع: <strong style="color:var(--success)">${fmtMoney(m.paidAmount || 0)}</strong></span>
        <span>المتأخرات: <strong>${fmtMoney(overdue)}</strong></span>
      </div>
      <div class="progress-wrap" style="margin-top:10px">
        <div class="progress-bar" style="height:8px;background:#fee2e2">
          <div style="height:100%;width:${paidPct}%;background:linear-gradient(90deg,var(--success),#34d399);border-radius:3px;transition:width .5s"></div>
        </div>
        <div class="progress-text">${paidPct}% مدفوع — ${100 - paidPct}% متبقي</div>
      </div>
      <button class="btn-sm" style="margin-top:12px;background:#fef2f2;color:var(--danger);border:1px solid #fca5a5;border-radius:8px;cursor:pointer;font-family:inherit;font-weight:700" onclick="closeModal('memberDetailModal');goDelinquent()">
        ⚠️ عرض جميع المتأخرين
      </button>` : '✅ الاشتراك محدّث — لا يوجد أي متأخرات'}
    </div>
  `;

  document.getElementById('memberDetailFooter').innerHTML = `
    <button class="btn-secondary" onclick="closeModal('memberDetailModal')">إغلاق</button>
    <button class="btn-secondary" onclick="closeModal('memberDetailModal');editMember(${m.id})">✎ تعديل البيانات</button>
    <button class="btn-primary" onclick="closeModal('memberDetailModal');openMemberPayModal(${m.id},'${m.fullName}')">💳 تسجيل دفع</button>
  `;

  openModal('memberDetailModal');
}

function goDelinquent() {
  // الانتقال لتبويب المتأخرين في صفحة الأعضاء
  navigate('members');
  S.members.filter = 'delinquent';
  const tabs = document.querySelectorAll('#memberTabs .tab');
  tabs.forEach(t => t.classList.toggle('active', t.dataset.filter === 'delinquent'));
  loadMembers();
}

function calcOverdue() {
  const total = parseFloat(document.getElementById('mTotalDue').value) || 0;
  const paid  = parseFloat(document.getElementById('mPaidAmount').value) || 0;
  const overdue = Math.max(0, total - paid);
  document.getElementById('overdueAmount').textContent = fmtMoney(overdue);
  const statusEl = document.getElementById('overdueStatus');
  if (overdue > 0) {
    statusEl.textContent = '⚠️ يوجد متأخرات';
    statusEl.className = 'overdue-warn';
    document.getElementById('overdueDisplay').className = 'overdue-display has-overdue';
  } else {
    statusEl.textContent = '✓ لا يوجد متأخرات';
    statusEl.className = 'overdue-ok';
    document.getElementById('overdueDisplay').className = 'overdue-display';
  }
}

function openMemberModal(member) {
  document.getElementById('memberForm').reset();
  document.getElementById('memberId').value = '';
  document.getElementById('memberModalTitle').textContent = 'عضو جديد';
  document.getElementById('overdueDisplay').className = 'overdue-display';
  document.getElementById('overdueAmount').textContent = '0.00 ر.ع';
  document.getElementById('overdueStatus').textContent = '✓ لا يوجد متأخرات';
  document.getElementById('overdueStatus').className = 'overdue-ok';
  // تعيين تاريخ اليوم كافتراضي
  document.getElementById('mLastPaymentDate').value = new Date().toISOString().substring(0, 10);

  if (member) {
    document.getElementById('memberModalTitle').textContent = 'تعديل عضو';
    document.getElementById('memberId').value          = member.id;
    document.getElementById('mFullName').value         = member.fullName;
    document.getElementById('mPhone').value            = member.phoneNumber;
    document.getElementById('mNationalId').value       = member.nationalId || '';
    document.getElementById('mAddress').value          = member.address || '';
    document.getElementById('mSubscription').value     = member.monthlySubscription || '';
    document.getElementById('mTotalDue').value         = member.totalPaymentDue || '';
    document.getElementById('mPaidAmount').value       = member.paidAmount || '';
    document.getElementById('mLastPaymentDate').value  = member.lastPaymentDate
      ? new Date(member.lastPaymentDate).toISOString().substring(0, 10)
      : new Date().toISOString().substring(0, 10);
    calcOverdue();
  }
  openModal('memberModal');
}

async function editMember(id) {
  try {
    const m = await API.get(`/members/${id}`);
    openMemberModal(m);
  } catch (err) { handleErr(err); }
}

async function submitMember() {
  const id         = document.getElementById('memberId').value;
  const fullName   = document.getElementById('mFullName').value.trim();
  const phoneNumber= document.getElementById('mPhone').value.trim();
  const totalDue   = parseFloat(document.getElementById('mTotalDue').value) || 0;
  const paidAmount = parseFloat(document.getElementById('mPaidAmount').value) || 0;
  const lastDate   = document.getElementById('mLastPaymentDate').value || undefined;

  if (!fullName)    { toast('أدخل الاسم الكامل', 'error'); return; }
  if (!phoneNumber) { toast('أدخل رقم الجوال', 'error'); return; }
  if (paidAmount > totalDue && totalDue > 0) {
    toast('المبلغ المدفوع لا يمكن أن يتجاوز الإجمالي', 'error'); return;
  }

  const body = {
    fullName,
    phoneNumber,
    nationalId:          document.getElementById('mNationalId').value.trim() || undefined,
    address:             document.getElementById('mAddress').value.trim() || undefined,
    monthlySubscription: parseFloat(document.getElementById('mSubscription').value) || 0,
    totalPaymentDue:     totalDue,
    paidAmount:          paidAmount,
    lastPaymentDate:     lastDate,
  };

  try {
    if (id) await API.put('/members/' + id, body);
    else    await API.post('/members', body);
    invalidateDashCache();
    toast(id ? 'تم تحديث بيانات العضو' : 'تم إضافة العضو بنجاح', 'success');
    closeModal('memberModal');
    loadMembers();
  } catch (err) { handleErr(err); }
}

async function deleteMember(id, name) {
  confirm('حذف العضو', `هل تريد حذف العضو "${name}"؟`, async () => {
    try {
      await API.delete('/members/' + id);
      invalidateDashCache();
      toast('تم حذف العضو', 'success');
      loadMembers();
    } catch (err) { handleErr(err); }
  });
}

function openMemberPayModal(memberId, fullName) {
  document.getElementById('memberPayId').value  = memberId;
  document.getElementById('memberPayName').textContent = `العضو: ${fullName}`;
  const now = new Date();
  document.getElementById('payYear').value  = now.getFullYear();
  document.getElementById('payMonth').value = now.getMonth() + 1;
  document.getElementById('payAmount').value = '';
  openModal('memberPayModal');
}

async function submitMemberPay() {
  const memberId = document.getElementById('memberPayId').value;
  const year   = parseInt(document.getElementById('payYear').value);
  const month  = parseInt(document.getElementById('payMonth').value);
  const amount = parseFloat(document.getElementById('payAmount').value);
  if (!amount || amount <= 0) { toast('أدخل مبلغاً صحيحاً', 'error'); return; }
  try {
    await API.post(`/members/${memberId}/payments`, { year, month, amount });
    toast(`تم تسجيل دفع ${monthNames[month]} ${year}`, 'success');
    closeModal('memberPayModal');
    loadMembers();
  } catch (err) { handleErr(err); }
}

/* ════════════════════════════════════════════════════════════════
   COMPLAINTS
════════════════════════════════════════════════════════════════ */
async function loadComplaints() {
  const listEl   = document.getElementById('complaintsList');
  const tabsEl   = document.getElementById('complaintTabs');
  listEl.innerHTML = '<div class="loading-center">جارٍ التحميل...</div>';

  if (!can('viewAllComplaints')) {
    // المستخدم العادي: يرى فقط الرسائل المنشورة للعموم
    tabsEl.style.display = 'none';
    try {
      const data = await API.get('/complaints/public');
      S.complaints.data = Array.isArray(data) ? data : [];
      renderComplaints();
    } catch (err) { handleErr(err); }
    return;
  }

  tabsEl.style.display = '';
  try {
    const data = await API.get('/complaints');
    S.complaints.data = Array.isArray(data) ? data : [];
    renderComplaints();
  } catch (err) { handleErr(err); }
}

document.getElementById('complaintTabs').addEventListener('click', e => {
  const tab = e.target.closest('.tab');
  if (!tab) return;
  document.querySelectorAll('#complaintTabs .tab').forEach(t => t.classList.remove('active'));
  tab.classList.add('active');
  S.complaints.status = tab.dataset.status;
  renderComplaints();
});

function renderComplaints() {
  const el = document.getElementById('complaintsList');
  let rows = S.complaints.data;
  if (S.complaints.status) rows = rows.filter(c => c.status === S.complaints.status);
  if (!rows.length) { el.innerHTML = '<div class="empty-state"><div class="empty-icon">📭</div><p>لا توجد شكاوى</p></div>'; return; }

  el.innerHTML = rows.map(c => {
    const stClass = { New: 'badge-new', UnderReview: 'badge-review', Resolved: 'badge-resolved' }[c.status] || '';
    const stLabel = { New: 'جديد', UnderReview: 'قيد المراجعة', Resolved: 'تم الحل' }[c.status] || c.status;
    const typeClass = c.type === 'Suggestion' ? 'badge-suggestion' : 'badge-complaint';
    const typeLabel = c.type === 'Suggestion' ? '💡 اقتراح' : '🚨 شكوى';
    return `
    <div class="complaint-card">
      <div class="complaint-card-header">
        <h4>${c.title}</h4>
        <div style="display:flex;gap:6px;flex-wrap:wrap">
          ${badge(typeClass, typeLabel)}
          ${badge(stClass, stLabel)}
        </div>
      </div>
      <div class="complaint-meta">
        <span>👤 ${c.isAnonymous ? 'مجهول' : (c.senderName || 'مجهول')}</span>
        <span>📅 ${fmtDate(c.createdAt)}</span>
        ${c.respondedAt ? `<span>↩ ${fmtDate(c.respondedAt)}</span>` : ''}
      </div>
      <div class="complaint-content">${c.content}</div>
      ${c.adminResponse ? `<div class="complaint-response"><strong>رد الإدارة:</strong> ${c.adminResponse}</div>` : ''}
      <div style="margin-top:12px;display:flex;gap:8px;flex-wrap:wrap;align-items:center">
        ${can('respondComplaints') && c.status !== 'Resolved' ? `<button class="btn-secondary btn-sm" onclick="openComplaintModal(${c.id})">↩ رد على الملاحظة</button>` : ''}
        ${isAdmin() ? `<button class="btn-sm ${c.isPublic ? 'btn-publish-active' : 'btn-publish'}" onclick="togglePublish(${c.id})" id="publishBtn-${c.id}">
          ${c.isPublic ? '🌐 منشور للعموم' : '📢 نشر للعموم'}
        </button>` : ''}
      </div>
    </div>`;
  }).join('');
}

let _currentComplaintId = null;
function openComplaintModal(id) {
  _currentComplaintId = id;
  const c = S.complaints.data.find(x => x.id === id);
  document.getElementById('complaintModalBody').innerHTML = `
    <div class="detail-section" style="margin-bottom:16px">
      <h4>${c.title}</h4>
      <p style="font-size:13.5px;color:var(--text-2);line-height:1.7">${c.content}</p>
    </div>
    <div class="field-group">
      <label>الرد <span class="req">*</span></label>
      <textarea id="complaintResponse" class="form-control" rows="4" placeholder="اكتب ردك هنا..."></textarea>
    </div>
    <div class="field-group">
      <label>تحديث الحالة</label>
      <select id="complaintStatus" class="form-control">
        <option value="UnderReview">قيد المراجعة</option>
        <option value="Resolved" selected>تم الحل</option>
      </select>
    </div>`;
  openModal('complaintModal');
}

async function togglePublish(id) {
  try {
    const res = await API.patch(`/complaints/${id}/publish`);
    toast(res.message, 'success');
    // تحديث الزر مباشرة بدون إعادة تحميل كاملة
    const c = S.complaints.data.find(x => x.id === id);
    if (c) {
      c.isPublic = res.isPublic;
      const btn = document.getElementById(`publishBtn-${id}`);
      if (btn) {
        btn.className = `btn-sm ${res.isPublic ? 'btn-publish-active' : 'btn-publish'}`;
        btn.innerHTML = res.isPublic ? '🌐 منشور للعموم' : '📢 نشر للعموم';
      }
    }
    // تحديث اللوحة العامة في الداشبورد
    loadPublicBoard();
  } catch (err) { handleErr(err); }
}

async function submitComplaintResponse() {
  const response = document.getElementById('complaintResponse').value.trim();
  const status   = document.getElementById('complaintStatus').value;
  if (!response) { toast('أدخل نص الرد', 'error'); return; }
  try {
    await API.patch(`/complaints/${_currentComplaintId}/respond`, { adminResponse: response, status });
    invalidateDashCache();
    refreshBadges();
    toast('تم إرسال الرد', 'success');
    closeModal('complaintModal');
    loadComplaints();
  } catch (err) { handleErr(err); }
}

/* ════════════════════════════════════════════════════════════════
   USERS
════════════════════════════════════════════════════════════════ */
// ── الصلاحيات الموحّدة ─────────────────────────────────────────────────────
const UNIFIED_KEYS = [
  ['uViewBookings','viewBookings'], ['uCreateBookings','createBookings'],
  ['uConfirmBookings','confirmBookings'], ['uDeleteBookings','deleteBookings'],
  ['uViewMembers','viewMembers'], ['uManageMembers','manageMembers'],
  ['uViewAllComplaints','viewAllComplaints'], ['uRespondComplaints','respondComplaints'],
  ['uViewDashboard','viewDashboard'], ['uViewReports','viewReports'],
];

const DEFAULT_UNIFIED = {
  viewDashboard:true, viewBookings:true, createBookings:true,
  confirmBookings:false, deleteBookings:false,
  viewMembers:false, manageMembers:false,
  viewAllComplaints:false, respondComplaints:false, viewReports:false,
};

function loadUnifiedPerms() {
  const saved = localStorage.getItem('unifiedPerms');
  const perms = saved ? JSON.parse(saved) : DEFAULT_UNIFIED;
  UNIFIED_KEYS.forEach(([elId, key]) => {
    const el = document.getElementById(elId);
    if (el) el.checked = perms[key] ?? false;
  });
}

function saveUnifiedDraft() {
  const perms = {};
  UNIFIED_KEYS.forEach(([elId, key]) => {
    const el = document.getElementById(elId);
    if (el) perms[key] = el.checked;
  });
  localStorage.setItem('unifiedPerms', JSON.stringify(perms));
}

function getUnifiedPerms() {
  const saved = localStorage.getItem('unifiedPerms');
  return saved ? JSON.parse(saved) : DEFAULT_UNIFIED;
}

async function applyUnifiedPerms() {
  const perms = getUnifiedPerms();
  const btn = document.getElementById('unifyBtnText');
  btn.textContent = 'جارٍ التطبيق...';
  try {
    const res = await API.put('/users/permissions/all', perms);
    toast(res.message, 'success');
    // تحديث الكاش المحلي لجميع المستخدمين
    S.users.data.filter(u => u.role !== 'Admin').forEach(u => {
      if (!S._userPerms) S._userPerms = {};
      S._userPerms[u.id] = perms;
    });
    renderUsersTable();
  } catch (err) { handleErr(err); }
  finally { btn.textContent = 'تطبيق على الجميع'; }
}

// ── تحميل المستخدمين ────────────────────────────────────────────────────────
async function loadUsers() {
  document.getElementById('usersTable').innerHTML = '<div class="loading-center">جارٍ التحميل...</div>';
  loadUnifiedPerms();
  try {
    const data = await API.get('/users');
    S.users.data = data;
    renderUsersStats(data);
    renderUsersTable();
  } catch (err) { handleErr(err); }
  loadLoginLogs();
}

const LOGS_PAGE_SIZE = 10;
let _logsPage = 1;

const _trashSvg = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="15" height="15"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/></svg>`;

async function loadLoginLogs(page = _logsPage) {
  _logsPage = page;
  const wrap = document.getElementById('loginLogsTable');
  if (!wrap) return;
  wrap.innerHTML = '<div class="loading-center">جارٍ التحميل...</div>';
  try {
    const { total, logs } = await API.get(`/users/login-logs?page=${page}&pageSize=${LOGS_PAGE_SIZE}`);
    if (!logs.length && page === 1) { wrap.innerHTML = '<div class="empty-state">لا توجد سجلات دخول بعد</div>'; return; }

    const totalPages = Math.ceil(total / LOGS_PAGE_SIZE);

    const logItems = logs.map(l => {
      const d       = new Date(l.loginAt);
      const date    = d.toLocaleDateString('ar-SA', { year:'numeric', month:'short', day:'numeric' });
      const time    = d.toLocaleTimeString('ar-SA', { hour:'2-digit', minute:'2-digit' });
      const isAdmin = l.userRole === 'Admin';
      const initials = (l.userName || '؟').trim()[0];
      const ipHtml  = l.ipAddress ? `<span class="log-ip">${l.ipAddress}</span>` : '';
      return `
      <div class="log-item" data-id="${l.id}">
        <label class="log-check-wrap">
          <input type="checkbox" class="log-cb" data-id="${l.id}">
          <span class="log-check-box"></span>
        </label>
        <div class="log-av ${isAdmin ? 'log-av-admin' : 'log-av-user'}">${initials}</div>
        <div class="log-info">
          <div class="log-name">${escHtml(l.userName)}</div>
          <div class="log-email">${escHtml(l.userEmail)}</div>
        </div>
        <div class="log-meta">
          ${isAdmin ? '<span class="badge badge-purple">مدير</span>' : '<span class="badge badge-blue">مستخدم</span>'}
          ${ipHtml}
        </div>
        <div class="log-time-block">
          <div class="log-date">${date}</div>
          <div class="log-hour">${time}</div>
        </div>
        <button class="log-del-btn del-log-btn" data-log-id="${l.id}" title="حذف">${_trashSvg}</button>
      </div>`;
    }).join('');

    // pagination buttons
    const pages = [];
    for (let p = 1; p <= totalPages; p++) {
      if (p === 1 || p === totalPages || Math.abs(p - page) <= 1) {
        pages.push(`<button class="page-btn ${p === page ? 'active' : ''}" data-log-page="${p}">${p}</button>`);
      } else if (pages[pages.length - 1] !== '…') {
        pages.push('…');
      }
    }

    const pagerHtml = totalPages > 1 ? `
      <div class="pagination" style="border-top:1px solid var(--border);margin-top:12px;padding-top:14px">
        <button class="page-btn" data-log-page="${page - 1}" ${page <= 1 ? 'disabled' : ''}>‹</button>
        ${pages.join('')}
        <button class="page-btn" data-log-page="${page + 1}" ${page >= totalPages ? 'disabled' : ''}>›</button>
        <span style="font-size:12px;color:var(--text-3);margin-right:8px">صفحة ${page} من ${totalPages}</span>
      </div>` : '';

    wrap.innerHTML = `
      <div class="log-list-header">
        <span class="log-total-badge">${total} سجل</span>
        <label class="log-select-all-wrap">
          <input type="checkbox" id="logSelectAll">
          <span class="log-check-box"></span>
          <span class="log-select-all-label">تحديد الكل</span>
        </label>
        <button class="log-bulk-del-btn" id="logBulkDelBtn" style="display:none">
          ${_trashSvg} حذف المحدد (<span id="logSelCount">0</span>)
        </button>
      </div>
      <div class="log-list">${logItems}</div>
      ${pagerHtml}`;

    // ── checkboxes logic ──
    const allCbs   = () => [...wrap.querySelectorAll('.log-cb')];
    const selCount = () => allCbs().filter(c => c.checked).length;
    const updateBulkBar = () => {
      const n = selCount();
      const btn = wrap.querySelector('#logBulkDelBtn');
      const countEl = wrap.querySelector('#logSelCount');
      if (btn) { btn.style.display = n > 0 ? '' : 'none'; if (countEl) countEl.textContent = n; }
      wrap.querySelectorAll('.log-item').forEach(item => {
        const cb = item.querySelector('.log-cb');
        item.classList.toggle('log-item-selected', cb?.checked);
      });
    };

    wrap.querySelector('#logSelectAll')?.addEventListener('change', e => {
      allCbs().forEach(cb => { cb.checked = e.target.checked; });
      updateBulkBar();
    });
    allCbs().forEach(cb => cb.addEventListener('change', () => {
      const all = wrap.querySelector('#logSelectAll');
      if (all) all.checked = allCbs().every(c => c.checked);
      updateBulkBar();
    }));

    // ── bulk delete ──
    wrap.querySelector('#logBulkDelBtn')?.addEventListener('click', async () => {
      const ids = allCbs().filter(c => c.checked).map(c => parseInt(c.dataset.id));
      if (!ids.length) return;
      const btn = wrap.querySelector('#logBulkDelBtn');
      btn.disabled = true;
      try {
        await API.req('DELETE', '/users/login-logs', ids);
        toast(`تم حذف ${ids.length} سجل`, 'success');
        loadLoginLogs(_logsPage);
      } catch (err) { toast(err.message, 'error'); btn.disabled = false; }
    });

    // ── single delete ──
    wrap.querySelectorAll('.del-log-btn').forEach(btn => {
      btn.addEventListener('click', async () => {
        const id = btn.dataset.logId;
        if (btn.dataset.confirm !== '1') {
          btn.dataset.confirm = '1';
          btn.innerHTML = '<span style="font-size:11px;font-weight:700">تأكيد؟</span>';
          btn.classList.add('log-del-confirm');
          setTimeout(() => {
            if (btn.dataset.confirm === '1') {
              btn.dataset.confirm = '';
              btn.innerHTML = _trashSvg;
              btn.classList.remove('log-del-confirm');
            }
          }, 3000);
          return;
        }
        btn.disabled = true;
        try {
          await API.delete(`/users/login-logs/${id}`);
          toast('تم حذف السجل', 'success');
          loadLoginLogs(_logsPage);
        } catch (err) { toast(err.message, 'error'); btn.disabled = false; }
      });
    });

    wrap.querySelectorAll('[data-log-page]').forEach(btn => {
      btn.addEventListener('click', () => {
        const p = parseInt(btn.dataset.logPage);
        if (!isNaN(p) && p >= 1 && p <= totalPages) loadLoginLogs(p);
      });
    });

  } catch (err) { wrap.innerHTML = `<div class="empty-state">تعذّر تحميل السجلات</div>`; }
}

function renderUsersStats(data) {
  const total   = data.length;
  const admins  = data.filter(u => u.role === 'Admin').length;
  const inactive = data.filter(u => !u.isActive).length;
  const grid = document.getElementById('usersStats');
  grid.innerHTML = [
    { icon: '👤', label: 'إجمالي المستخدمين', val: total,    color: '#2563eb', bg: '#eff6ff' },
    { icon: '🛡',  label: 'المدراء',            val: admins,   color: '#7c3aed', bg: '#f5f3ff' },
    { icon: '🚫',  label: 'حسابات موقوفة',      val: inactive, color: '#dc2626', bg: '#fef2f2' },
  ].map(c => `
    <div class="stat-card" style="--stat-color:${c.color};--stat-bg:${c.bg}">
      <div class="stat-card-row">
        <div class="stat-text">
          <div class="stat-label">${c.label}</div>
          <div class="stat-value">${c.val}</div>
        </div>
        <div class="stat-icon-bubble">${c.icon}</div>
      </div>
      <div class="stat-bottom-bar"></div>
    </div>`).join('');
}

document.getElementById('userRoleTabs').addEventListener('click', e => {
  const tab = e.target.closest('.tab');
  if (!tab) return;
  document.querySelectorAll('#userRoleTabs .tab').forEach(t => t.classList.remove('active'));
  tab.classList.add('active');
  S.users.role = tab.dataset.role;
  renderUsersTable();
});

function filterUsers() {
  S.users.search = document.getElementById('userSearch').value.toLowerCase();
  S.users.status = document.getElementById('userStatusFilter').value;
  renderUsersTable();
}

function renderUsersTable() {
  const el = document.getElementById('usersTable');
  let rows = S.users.data;

  if (S.users.role)   rows = rows.filter(u => u.role === S.users.role);
  if (S.users.search) rows = rows.filter(u =>
    u.fullName.toLowerCase().includes(S.users.search) ||
    u.email.toLowerCase().includes(S.users.search) ||
    u.phoneNumber.includes(S.users.search));
  if (S.users.status === 'active')   rows = rows.filter(u => u.isActive);
  if (S.users.status === 'inactive') rows = rows.filter(u => !u.isActive);

  if (!rows.length) {
    el.innerHTML = '<div class="empty-state"><div class="empty-icon">👤</div><p>لا توجد نتائج</p></div>';
    return;
  }

  const myId = S.user?.id || 0;

  el.innerHTML = rows.map(u => {
    const isMe   = u.id === myId;
    const isAdm  = u.role === 'Admin';
    const ini    = u.fullName.charAt(0);
    const cached = S._userPerms?.[u.id];

    // شارات الصلاحيات الرئيسية (يتم تحميلها إذا موجودة في الكاش)
    let permsHtml = '';
    if (isAdm) {
      permsHtml = `<span class="uc-perm-tag" style="background:#fef9c3;color:#92400e;border-color:#fcd34d">صلاحيات كاملة</span>`;
    } else if (cached) {
      const count = Object.values(cached).filter(Boolean).length;
      permsHtml = `<span class="uc-perm-count">${count} صلاحية مفعّلة</span>`;
    }

    return `
    <div class="user-card-row">
      <div class="uc-avatar ${isAdm ? 'admin' : 'user'}">${ini}</div>
      <div class="uc-info">
        <div class="uc-name">
          ${escHtml(u.fullName)}
          ${isMe ? '<span style="font-size:11px;color:var(--gold)">(أنت)</span>' : ''}
          <span class="uc-role-badge ${isAdm ? 'admin' : 'user'}">${isAdm ? '🛡 مدير' : '👤 مستخدم'}</span>
          ${!u.isActive ? '<span class="badge badge-cancelled" style="font-size:11px">موقوف</span>' : ''}
        </div>
        <div class="uc-meta">${escHtml(u.email)} · ${u.phoneNumber} · ${u.bookingsCount} حجز · ${fmtDate(u.createdAt)}</div>
        ${permsHtml ? `<div class="uc-perms">${permsHtml}</div>` : ''}
      </div>
      <div class="uc-actions">
        ${!isMe && !isAdm
          ? `<button class="uc-action-btn perm" title="الصلاحيات" onclick="openPermissionsModal(${u.id},'${escHtml(u.fullName)}')">🔐</button>` : ''}
        ${!isMe
          ? `<button class="uc-action-btn pass" title="إعادة تعيين كلمة المرور" onclick="openResetPass(${u.id},'${escHtml(u.fullName)}')">🔑</button>` : ''}
        ${!isMe && !isAdm
          ? `<button class="uc-action-btn ${u.isActive ? 'del' : 'pass'}" title="${u.isActive ? 'إيقاف' : 'تفعيل'}" onclick="toggleUser(${u.id},${u.isActive})">
               ${u.isActive ? '⏸' : '▶'}
             </button>` : ''}
        ${!isMe
          ? `<button class="uc-action-btn del" title="حذف" onclick="deleteUser(${u.id},'${escHtml(u.fullName)}')">🗑</button>` : ''}
      </div>
    </div>`;
  }).join('');
}

async function changeRole(id, role, selectEl) {
  const prev = role === 'Admin' ? 'User' : 'Admin';
  try {
    await API.patch(`/users/${id}/role`, { role });
    selectEl.className = `role-select ${role === 'Admin' ? 'admin' : 'user'}`;
    toast(`تم تغيير الصلاحية إلى ${role === 'Admin' ? 'مدير' : 'مستخدم'}`, 'success');
    // تحديث البيانات المحلية
    const u = S.users.data.find(x => x.id === id);
    if (u) u.role = role;
    renderUsersStats(S.users.data);
  } catch (err) {
    selectEl.value = prev;
    toast(err.message, 'error');
  }
}

async function toggleUser(id, currentActive) {
  const action = currentActive ? 'إيقاف' : 'تفعيل';
  confirm(`${action} الحساب`, `هل تريد ${action} هذا الحساب؟`, async () => {
    try {
      const res = await API.patch(`/users/${id}/toggle`);
      toast(res.message, 'success');
      const u = S.users.data.find(x => x.id === id);
      if (u) u.isActive = !u.isActive;
      renderUsersStats(S.users.data);
      renderUsersTable();
    } catch (err) { handleErr(err); }
  });
}

function openResetPass(id, name) {
  document.getElementById('resetPassUserId').value = id;
  document.getElementById('resetPassName').textContent = `المستخدم: ${name}`;
  document.getElementById('resetPassNew').value = '';
  openModal('resetPassModal');
}

async function submitResetPass() {
  const id  = document.getElementById('resetPassUserId').value;
  const pwd = document.getElementById('resetPassNew').value;
  if (!pwd || pwd.length < 6) { toast('كلمة المرور يجب أن تكون 6 أحرف على الأقل', 'error'); return; }
  try {
    const res = await API.patch(`/users/${id}/reset-password`, { newPassword: pwd });
    toast(res.message, 'success');
    closeModal('resetPassModal');
  } catch (err) { handleErr(err); }
}

async function deleteUser(id, name) {
  confirm('حذف المستخدم', `هل تريد حذف "${name}" نهائياً؟`, async () => {
    try {
      await API.delete(`/users/${id}`);
      toast('تم حذف المستخدم', 'success');
      S.users.data = S.users.data.filter(u => u.id !== id);
      renderUsersStats(S.users.data);
      renderUsersTable();
    } catch (err) { handleErr(err); }
  });
}

function openAddUserModal() {
  document.getElementById('addUserForm').reset();
  openModal('addUserModal');
}

async function openPermissionsModal(userId, userName) {
  document.getElementById('permUserId').value = userId;
  document.getElementById('permUserName').textContent = userName;
  document.getElementById('permUserAvatar').textContent = userName.charAt(0);
  try {
    const p = await API.get(`/users/${userId}/permissions`);
    // كاش محلي لعرض الملخص في الجدول
    if (!S._userPerms) S._userPerms = {};
    S._userPerms[userId] = p;

    document.getElementById('pViewBookings').checked      = p.viewBookings;
    document.getElementById('pCreateBookings').checked    = p.createBookings;
    document.getElementById('pConfirmBookings').checked   = p.confirmBookings;
    document.getElementById('pDeleteBookings').checked    = p.deleteBookings;
    document.getElementById('pViewMembers').checked       = p.viewMembers;
    document.getElementById('pManageMembers').checked     = p.manageMembers;
    document.getElementById('pViewAllComplaints').checked = p.viewAllComplaints;
    document.getElementById('pRespondComplaints').checked = p.respondComplaints;
    document.getElementById('pViewDashboard').checked     = p.viewDashboard;
    document.getElementById('pViewReports').checked       = p.viewReports;
    updatePermCount();
    openModal('permissionsModal');
  } catch (err) { handleErr(err); }
}

function updatePermCount() {
  const total   = PERM_IDS.length;
  const enabled = PERM_IDS.filter(id => document.getElementById(id)?.checked).length;
  document.getElementById('permEnabledCount').textContent  = enabled;
  document.getElementById('permDisabledCount').textContent = total - enabled;
}

const PERM_IDS = [
  'pViewBookings','pCreateBookings','pConfirmBookings','pDeleteBookings',
  'pViewMembers','pManageMembers',
  'pViewAllComplaints','pRespondComplaints',
  'pViewDashboard','pViewReports'
];

const PERM_PRESETS = {
  none:    { pViewBookings:false,pCreateBookings:false,pConfirmBookings:false,pDeleteBookings:false,
             pViewMembers:false,pManageMembers:false,
             pViewAllComplaints:false,pRespondComplaints:false,pViewDashboard:false,pViewReports:false },
  // الصلاحيات الافتراضية عند إنشاء حساب جديد
  default: { pViewBookings:true, pCreateBookings:true, pConfirmBookings:false,pDeleteBookings:false,
             pViewMembers:false,pManageMembers:false,
             pViewAllComplaints:false,pRespondComplaints:false,pViewDashboard:true, pViewReports:false },
  view:    { pViewBookings:true, pCreateBookings:false,pConfirmBookings:false,pDeleteBookings:false,
             pViewMembers:true, pManageMembers:false,
             pViewAllComplaints:false,pRespondComplaints:false,pViewDashboard:true, pViewReports:false },
  standard:{ pViewBookings:true, pCreateBookings:true, pConfirmBookings:true, pDeleteBookings:false,
             pViewMembers:true, pManageMembers:false,
             pViewAllComplaints:true, pRespondComplaints:false,pViewDashboard:true, pViewReports:false },
  full:    { pViewBookings:true, pCreateBookings:true, pConfirmBookings:true, pDeleteBookings:true,
             pViewMembers:true, pManageMembers:true,
             pViewAllComplaints:true, pRespondComplaints:true, pViewDashboard:true, pViewReports:true },
};

function applyPermPreset(name) {
  const preset = PERM_PRESETS[name];
  if (!preset) return;
  PERM_IDS.forEach(id => {
    const el = document.getElementById(id);
    if (el) el.checked = preset[id] ?? false;
  });
  updatePermCount();
}

function toggleSection(section, value) {
  document.querySelectorAll(`#permissionsModal input[data-section="${section}"]`)
    .forEach(cb => cb.checked = value);
  updatePermCount();
}

async function savePermissions() {
  const id = document.getElementById('permUserId').value;
  const body = {
    viewBookings:      document.getElementById('pViewBookings').checked,
    createBookings:    document.getElementById('pCreateBookings').checked,
    confirmBookings:   document.getElementById('pConfirmBookings').checked,
    deleteBookings:    document.getElementById('pDeleteBookings').checked,
    viewMembers:       document.getElementById('pViewMembers').checked,
    manageMembers:     document.getElementById('pManageMembers').checked,
    viewAllComplaints: document.getElementById('pViewAllComplaints').checked,
    respondComplaints: document.getElementById('pRespondComplaints').checked,
    viewDashboard:     document.getElementById('pViewDashboard').checked,
    viewReports:       document.getElementById('pViewReports').checked,
  };
  try {
    const res = await API.put(`/users/${id}/permissions`, body);
    // تحديث الكاش المحلي لعرض الملخص في الجدول فوراً
    if (!S._userPerms) S._userPerms = {};
    S._userPerms[id] = body;
    toast(res.message, 'success');
    closeModal('permissionsModal');
    renderUsersTable();
  } catch (err) { handleErr(err); }
}

async function submitAddUser() {
  const body = {
    fullName:    document.getElementById('auFullName').value.trim(),
    phoneNumber: document.getElementById('auPhone').value.trim(),
    email:       document.getElementById('auEmail').value.trim(),
    password:    document.getElementById('auPassword').value,
  };
  const role = document.getElementById('auRole').value;

  if (!body.fullName)    { toast('أدخل الاسم', 'error'); return; }
  if (!body.email)       { toast('أدخل البريد الإلكتروني', 'error'); return; }
  if (!body.password || body.password.length < 6) { toast('كلمة المرور يجب أن تكون 6 أحرف على الأقل', 'error'); return; }

  try {
    // تسجيل المستخدم أولاً
    const res = await API.post('/auth/register', body);
    // ثم تغيير دوره إذا كان Admin
    if (role === 'Admin') {
      // نحتاج Id المستخدم الجديد — نجلبه من القائمة
      await loadUsers();
      const newUser = S.users.data.find(u => u.email === body.email);
      if (newUser) await API.patch(`/users/${newUser.id}/role`, { role: 'Admin' });
    } else {
      await loadUsers();
    }
    toast('تم إضافة المستخدم بنجاح', 'success');
    closeModal('addUserModal');
  } catch (err) { handleErr(err); }
}

/* ════════════════════════════════════════════════════════════════
   Pagination
════════════════════════════════════════════════════════════════ */
function renderPagination(containerId, total, pageSize, current, onChange) {
  const el    = document.getElementById(containerId);
  const pages = Math.ceil(total / pageSize);
  if (pages <= 1) { el.innerHTML = ''; return; }
  let btns = '';
  if (current > 1) btns += `<button class="page-btn" onclick="(${onChange.toString()})(${current-1})">‹</button>`;
  for (let p = Math.max(1, current-2); p <= Math.min(pages, current+2); p++)
    btns += `<button class="page-btn ${p === current ? 'active' : ''}" onclick="(${onChange.toString()})(${p})">${p}</button>`;
  if (current < pages) btns += `<button class="page-btn" onclick="(${onChange.toString()})(${current+1})">›</button>`;
  el.innerHTML = btns;
}

/* ════════════════════════════════════════════════════════════════
   Init
════════════════════════════════════════════════════════════════ */
if (S.token && S.user) bootApp();
