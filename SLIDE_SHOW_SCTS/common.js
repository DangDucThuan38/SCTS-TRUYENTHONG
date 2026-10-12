// ===== Phiên đăng nhập =====
function getUser() {
    try { return JSON.parse(localStorage.getItem('currentUser')); }
    catch { return null; }
}

function getSite() {
    return getUser()?.site || ENV.DEFAULT_SITE;
}

/**
 * Bảo vệ trang. allowedRoles: mảng role được vào (bỏ trống = chỉ cần đăng nhập).
 * Trả về user, hoặc null nếu bị chuyển hướng.
 */
function requireAuth(allowedRoles) {
    const user = getUser();
    if (!user) {
        alert('Bạn chưa đăng nhập!');
        location.href = 'login.html';
        return null;
    }
    if (allowedRoles && !allowedRoles.includes(String(user.role))) {
        alert('Bạn không có quyền truy cập trang này!');
        location.href = 'home.html';
        return null;
    }
    return user;
}

function logout() {
    if (!confirm('Bạn có chắc chắn muốn đăng xuất?')) return;
    localStorage.removeItem('currentUser');
    location.href = 'login.html';
}

// ===== Gọi API =====
function apiUrl(path, params = {}) {
    const q = new URLSearchParams(params).toString();
    return ENV.API_URL + path + (q ? `?${q}` : '');
}

/** Tự gắn ?Site= (tắt bằng withSite:false). body là object -> JSON, FormData -> giữ nguyên. */
function apiFetch(path, { method = 'GET', params = {}, body, withSite = true } = {}) {
    const opts = { method, headers: { accept: '*/*' } };
    if (body instanceof FormData) {
        opts.body = body;
    } else if (body !== undefined) {
        opts.headers['Content-Type'] = 'application/json';
        opts.body = JSON.stringify(body);
    }
    const query = withSite ? { Site: getSite(), ...params } : params;
    return fetch(apiUrl(path, query), opts);
}

// ===== Tiện ích =====
const roleName = (role) => ENV.ROLES[String(role)] || 'Nhân viên';

const pad2 = (n) => String(n).padStart(2, '0');

const formatTime = (d) => d ? new Date(d).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) : '--:--';
const formatDate = (d) => d ? new Date(d).toLocaleDateString('vi-VN') : '--/--/----';

/** Date -> 'yyyy-MM-dd' theo giờ địa phương (cho input type="date") */
function toDateInput(value) {
    const d = new Date(value);
    return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
}

/** Tạo <option> từ mảng giá trị */
const optionsHtml = (list, firstLabel) =>
    (firstLabel !== undefined ? `<option value="">${firstLabel}</option>` : '') +
    list.map(v => `<option value="${v}">${v}</option>`).join('');
