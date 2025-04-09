document.addEventListener('DOMContentLoaded', function () {
    // User filtering
    document.getElementById('filter-all')?.addEventListener('click', function () {
        showAllUsers();
        setActiveFilter(this);
    });

    document.getElementById('filter-admin')?.addEventListener('click', function () {
        filterUsersByRole('admin');
        setActiveFilter(this);
    });

    document.getElementById('filter-staff')?.addEventListener('click', function () {
        filterUsersByRole('frontdesk', 'housekeeping');
        setActiveFilter(this);
    });

    document.getElementById('filter-guest')?.addEventListener('click', function () {
        filterUsersByRole('guest');
        setActiveFilter(this);
    });

    function showAllUsers() {
        document.querySelectorAll('.user-row').forEach(row => {
            row.style.display = '';
        });
    }

    function filterUsersByRole(...roles) {
        document.querySelectorAll('.user-row').forEach(row => {
            if (roles.some(role => row.classList.contains(role.toLowerCase()))) {
                row.style.display = '';
            } else {
                row.style.display = 'none';
            }
        });
    }

    function setActiveFilter(button) {
        document.querySelectorAll('.user-filter-buttons .btn').forEach(btn => {
            btn.classList.remove('active', 'btn-primary');
            btn.classList.add('btn-outline-primary');
        });
        button.classList.add('active', 'btn-primary');
        button.classList.remove('btn-outline-primary');
    }

    // Set "All Users" as default active filter
    const allUsersBtn = document.getElementById('filter-all');
    if (allUsersBtn) {
        allUsersBtn.classList.add('active', 'btn-primary');
        allUsersBtn.classList.remove('btn-outline-primary');
    }
});