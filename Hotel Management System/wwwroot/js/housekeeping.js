// housekeeping.js - Scripts for the housekeeping module

document.addEventListener('DOMContentLoaded', function () {
    // Room status counters - fetch via AJAX
    $.ajax({
        url: housekeepingUrls.getRoomStatusCounts,
        type: 'GET',
        success: function (data) {
            // Update badges with count data
            $('#needsCleaningBadge').text(data.needsCleaning || 0);
            $('#sidebarNeedsCleaning').text(data.needsCleaning || 0);
            $('#sidebarMaintenance').text(data.maintenance || 0);
            $('#sidebarCheckout').text(data.checkedOut || 0);

            // Highlight count if greater than 0
            if (data.needsCleaning > 0) {
                $('#needsCleaningBadge').removeClass('bg-warning').addClass('bg-danger');
                $('#sidebarNeedsCleaning').removeClass('bg-warning').addClass('bg-danger');
            }
        },
        error: function () {
            console.log('Error fetching room status counts');
        }
    });

    // Room filtering
    document.getElementById('filter-all')?.addEventListener('click', function () {
        showAllRooms();
        setActiveFilter(this);
    });

    document.getElementById('filter-needs-cleaning')?.addEventListener('click', function () {
        filterRoomsByStatus('needs-cleaning');
        setActiveFilter(this);
    });

    document.getElementById('filter-available')?.addEventListener('click', function () {
        filterRoomsByStatus('available');
        setActiveFilter(this);
    });

    document.getElementById('filter-occupied')?.addEventListener('click', function () {
        filterRoomsByStatus('occupied', 'booked');
        setActiveFilter(this);
    });

    document.getElementById('filter-maintenance')?.addEventListener('click', function () {
        filterRoomsByStatus('maintenance');
        setActiveFilter(this);
    });

    function showAllRooms() {
        document.querySelectorAll('.room-row').forEach(row => {
            row.style.display = '';
        });
    }

    function filterRoomsByStatus(...statuses) {
        document.querySelectorAll('.room-row').forEach(row => {
            if (statuses.some(status => row.classList.contains(status))) {
                row.style.display = '';
            } else {
                row.style.display = 'none';
            }
        });
    }

    function setActiveFilter(button) {
        document.querySelectorAll('.btn-group .btn').forEach(btn => {
            btn.classList.remove('active', 'btn-primary');
            btn.classList.add('btn-outline-primary');
        });
        button.classList.add('active', 'btn-primary');
        button.classList.remove('btn-outline-primary');
    }

    const allRoomsBtn = document.getElementById('filter-all');
    if (allRoomsBtn) {
        allRoomsBtn.classList.add('active', 'btn-primary');
        allRoomsBtn.classList.remove('btn-outline-primary');
    }

    // Cleaning checklist functionality
    const checklistItems = document.querySelectorAll('.cleaning-checklist .form-check-input');
    if (checklistItems.length > 0) {
        checklistItems.forEach(item => {
            item.addEventListener('change', function () {
                updateChecklistProgress();
            });
        });

        function updateChecklistProgress() {
            const checkedCount = document.querySelectorAll('.cleaning-checklist .form-check-input:checked').length;
            const totalCount = checklistItems.length;

            if (checkedCount === totalCount) {
                if (confirm('All tasks completed! Mark this room as cleaned?')) {
                    document.querySelector('form[action*="MarkCleaned"]')?.submit();
                }
            }
        }
    }
});