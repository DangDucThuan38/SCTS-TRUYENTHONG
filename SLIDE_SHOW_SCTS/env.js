const API_URL = 'https://truyenthong.scts.com.vn:8062';

const ENV = {
    API_URL,
    DEFAULT_SITE: 'SCTS',
    TIMEOUT: 5000,
    STATIC_URL: `${API_URL}/StaticData/`,
    MEDIA_BASE: `${API_URL}/TruyenThong`, // dùng nối link ảnh/video ở show.html
    SLIDE_DURATION: 17000,

    // File JSON cục bộ
    LOCAL: {
        NHANVIEN: 'nhanvien.json',
        QUANSO: 'quansohangngay.json'
    },

    // Toàn bộ endpoint (path tương đối so với API_URL)
    API: {
        LOGIN: '/TruyenThong/login',

        // Tài khoản
        GET_ACCOUNTS: '/TruyenThong/get-accounts',
        CREATE_USER: '/TruyenThong/create-user',
        DELETE_USER: (name) => `/TruyenThong/delete-user/${encodeURIComponent(name)}`,

        // Quân số
        GET_QUANSO: '/TruyenThong/get',
        SAVE_QUANSO: '/TruyenThong/save',

        // Lịch công tác
        GET_CONGTAC: '/TruyenThong/get-congtac',
        SAVE_CONGTAC: '/TruyenThong/save-congtac',
        DELETE_CONGTAC: (id) => `/TruyenThong/delete-congtac/${encodeURIComponent(id)}`,
        CHANGE_POSITION: '/TruyenThong/change-position',

        // Media
        GET_MEDIA: '/TruyenThong/get-media-list',
        UPLOAD_MEDIA: '/TruyenThong/upload-list',
        UPDATE_MEDIA: '/TruyenThong/update-media-list',
        DELETE_MEDIA: (name) => `/TruyenThong/delete-file/${encodeURIComponent(name)}`,
        SCTS_MEDIA_LIST: '/SCTS/get-media-list', // dùng cho slideshow (test.html)

        // Nhân viên
        SAVE_NHANVIEN: '/SCTS/save-nhanvien',
        DELETE_NHANVIEN: (id) => `/SCTS/delete-nhanvien/${encodeURIComponent(id)}`
    },

    ROLES: {
        '1': 'Quản trị viên',
        '2': 'Quản lý quân số',
        '3': 'Quản lý media',
        '4': 'Quản lý chung',
        '5': 'Lịch làm việc'
    },

    SITES: {
        ALL: 'Quản lý Chung',
        HLV: 'Hoàn Lộc Việt',
        SCTS: 'SCTS',
        ASN: 'Văn Phòng Công Chứng Châu Á',
        SULECO: 'Suleco',
        MYSON: 'Công ty Điện mặt trời Mỹ Sơn'
    },

    DEPARTMENTS: [
        'Giải pháp (Solution)',
        'Phòng HCNS',
        'Dịch vụ IT (ITMS)',
        'Phòng Pháp Chế',
        'Phòng Kế Toán',
        'Kinh doanh (Sale & Marketing)',
        'Vận Hành',
        'Ban Tổng Giám Đốc',
        'Ban Điều Hành'
    ]
};
