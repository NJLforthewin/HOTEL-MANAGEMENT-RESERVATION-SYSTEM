document.addEventListener("DOMContentLoaded", function () {
    var roomSelect = document.getElementById("RoomId");
    var priceInput = document.getElementById("PricePerNight");
    var totalPriceInput = document.getElementById("TotalPrice");
    var checkInInput = document.getElementById("CheckInDate");
    var checkOutInput = document.getElementById("CheckOutDate");

    if (document.getElementById("RoomPricesData")) {
        var roomPrices = JSON.parse(document.getElementById("RoomPricesData").textContent);

        function updatePrice() {
            var selectedRoomId = roomSelect.value;
            if (selectedRoomId && roomPrices[selectedRoomId]) {
                priceInput.value = roomPrices[selectedRoomId];
                calculateTotalPrice();
            } else {
                priceInput.value = "";
                totalPriceInput.value = "";
            }
        }

        function calculateTotalPrice() {
            var checkInDate = new Date(checkInInput.value);
            var checkOutDate = new Date(checkOutInput.value);
            if (checkInDate && checkOutDate && checkOutDate > checkInDate) {
                var totalDays = (checkOutDate - checkInDate) / (1000 * 60 * 60 * 24);
                totalPriceInput.value = (totalDays * parseFloat(priceInput.value)).toFixed(2);
            } else {
                totalPriceInput.value = "";
            }
        }

        if (roomSelect) roomSelect.addEventListener("change", updatePrice);
        if (checkInInput) checkInInput.addEventListener("change", calculateTotalPrice);
        if (checkOutInput) checkOutInput.addEventListener("change", calculateTotalPrice);
    }
    const form = document.querySelector("form");
    if (form && form.querySelector("input[name='BookingType']")) {
        form.addEventListener("submit", function (event) {
            const bookingType = form.querySelector("input[name='BookingType']").value;
            console.log("Booking Type Sent:", bookingType);
        });
    }

    const navbar = document.querySelector('.navbar');
    if (navbar) {
        if (window.scrollY > 50) {
            navbar.classList.add('navbar-shrink');
        }

        window.addEventListener('scroll', function () {
            if (window.scrollY > 50) {
                navbar.classList.add('navbar-shrink');
            } else {
                navbar.classList.remove('navbar-shrink');
            }
        });
    }

    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            if (this.getAttribute('href') !== '#') {
                e.preventDefault();

                const targetId = this.getAttribute('href');
                const targetElement = document.querySelector(targetId);

                if (targetElement) {
                    const navbarHeight = navbar ? navbar.offsetHeight : 0;
                    const targetPosition = targetElement.getBoundingClientRect().top + window.scrollY - navbarHeight;

                    window.scrollTo({
                        top: targetPosition,
                        behavior: 'smooth'
                    });
                }
            }
        });
    });

    const urlParams = new URLSearchParams(window.location.search);
    if (urlParams.get('scrollTo')) {
        const targetElement = document.getElementById(urlParams.get('scrollTo'));
        if (targetElement) {
            const navbarHeight = navbar ? navbar.offsetHeight : 0;
            const targetPosition = targetElement.getBoundingClientRect().top + window.scrollY - navbarHeight;

            window.scrollTo({
                top: targetPosition,
                behavior: 'smooth'
            });
        }
    }

    const dateInputs = document.querySelectorAll('input[type="date"]');
    if (dateInputs.length > 0) {
        const today = new Date().toISOString().split('T')[0];

        dateInputs.forEach(input => {
            if (input.id === "CheckInDate" || input.id === "checkin") {
                input.min = today;

                input.addEventListener('change', function () {
                    const checkoutInput = document.getElementById("CheckOutDate") || document.getElementById("checkout");
                    if (checkoutInput) {
                        checkoutInput.min = this.value;

                        if (checkoutInput.value && checkoutInput.value < this.value) {
                            const nextDay = new Date(this.value);
                            nextDay.setDate(nextDay.getDate() + 1);
                            checkoutInput.value = nextDay.toISOString().split('T')[0];
                        }
                    }
                });
            }
        });
    }
});
document.addEventListener('DOMContentLoaded', function () {
    // Handle room details page date inputs
    const checkIn = document.getElementById('check-in');
    const checkOut = document.getElementById('check-out');

    if (checkIn && checkOut) {
        const today = new Date().toISOString().split('T')[0];
        checkIn.min = today;

        checkIn.addEventListener('change', function () {
            checkOut.min = this.value;

            if (checkOut.value && checkOut.value < this.value) {
                const nextDay = new Date(this.value);
                nextDay.setDate(nextDay.getDate() + 1);
                checkOut.value = nextDay.toISOString().split('T')[0];
            }
        });
    }

    const thumbnails = document.querySelectorAll('.thumbnail-images img');
    const mainImage = document.querySelector('.main-image img');

    if (thumbnails.length > 0 && mainImage) {
        thumbnails.forEach(thumb => {
            thumb.addEventListener('click', function () {
                const newSrc = this.getAttribute('src');
                mainImage.src = newSrc;
            });
        });
    }
});

// Room Filtering for Housekeeping
function initRoomFiltering() {
    const filterAll = document.getElementById('filter-all');
    const filterNeedsCleaning = document.getElementById('filter-needs-cleaning');
    const filterAvailable = document.getElementById('filter-available');
    const filterOccupied = document.getElementById('filter-occupied');
    const filterMaintenance = document.getElementById('filter-maintenance');

    if (!filterAll) return; // Exit if we're not on a page with filters

    filterAll.addEventListener('click', function () {
        showAllRooms();
        setActiveFilter(this);
    });

    filterNeedsCleaning?.addEventListener('click', function () {
        filterRoomsByStatus('needs-cleaning');
        setActiveFilter(this);
    });

    filterAvailable?.addEventListener('click', function () {
        filterRoomsByStatus('available');
        setActiveFilter(this);
    });

    filterOccupied?.addEventListener('click', function () {
        filterRoomsByStatus('occupied', 'booked');
        setActiveFilter(this);
    });

    filterMaintenance?.addEventListener('click', function () {
        filterRoomsByStatus('maintenance');
        setActiveFilter(this);
    });

    // Initialize with "All" filter active
    filterAll.classList.add('active');
    if (filterAll.classList.contains('btn-outline-primary')) {
        filterAll.classList.remove('btn-outline-primary');
        filterAll.classList.add('btn-primary');
    }
}

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
        btn.classList.remove('active');

        // Reset button to outline state
        if (btn.classList.contains('btn-primary')) {
            btn.classList.remove('btn-primary');
            btn.classList.add('btn-outline-primary');
        } else if (btn.classList.contains('btn-warning')) {
            btn.classList.remove('btn-warning');
            btn.classList.add('btn-outline-warning');
        } else if (btn.classList.contains('btn-success')) {
            btn.classList.remove('btn-success');
            btn.classList.add('btn-outline-success');
        } else if (btn.classList.contains('btn-danger')) {
            btn.classList.remove('btn-danger');
            btn.classList.add('btn-outline-danger');
        } else if (btn.classList.contains('btn-secondary')) {
            btn.classList.remove('btn-secondary');
            btn.classList.add('btn-outline-secondary');
        }
    });

    button.classList.add('active');

    // Set active button to solid state
    if (button.classList.contains('btn-outline-primary')) {
        button.classList.remove('btn-outline-primary');
        button.classList.add('btn-primary');
    } else if (button.classList.contains('btn-outline-warning')) {
        button.classList.remove('btn-outline-warning');
        button.classList.add('btn-warning');
    } else if (button.classList.contains('btn-outline-success')) {
        button.classList.remove('btn-outline-success');
        button.classList.add('btn-success');
    } else if (button.classList.contains('btn-outline-danger')) {
        button.classList.remove('btn-outline-danger');
        button.classList.add('btn-danger');
    } else if (button.classList.contains('btn-outline-secondary')) {
        button.classList.remove('btn-outline-secondary');
        button.classList.add('btn-secondary');
    }
}

// Cleaning Checklist Functionality
function initCleaningChecklist() {
    const checklistItems = document.querySelectorAll('.cleaning-checklist .form-check-input');
    if (checklistItems.length === 0) return; // Exit if no checklist exists

    checklistItems.forEach(item => {
        item.addEventListener('change', function () {
            updateChecklistProgress();
        });
    });
}

function updateChecklistProgress() {
    const checklistItems = document.querySelectorAll('.cleaning-checklist .form-check-input');
    const checkedCount = document.querySelectorAll('.cleaning-checklist .form-check-input:checked').length;
    const totalCount = checklistItems.length;

    if (checkedCount === totalCount) {
        if (confirm('All tasks completed! Mark this room as cleaned?')) {
            document.querySelector('form[action*="MarkCleaned"]')?.submit();
        }
    }
}

// Room Status Counts for Housekeeping Dashboard
function fetchRoomStatusCounts() {
    const needsCleaningBadge = document.getElementById('needsCleaningBadge');
    const sidebarNeedsCleaning = document.getElementById('sidebarNeedsCleaning');
    const sidebarMaintenance = document.getElementById('sidebarMaintenance');
    const sidebarCheckout = document.getElementById('sidebarCheckout');

    if (!needsCleaningBadge && !sidebarNeedsCleaning) return; // Exit if not on housekeeping dashboard

    const url = housekeepingUrls?.getRoomStatusCounts;
    if (!url) return;

    $.ajax({
        url: url,
        type: 'GET',
        success: function (data) {
            // Update badges with count data
            if (needsCleaningBadge) needsCleaningBadge.textContent = data.needsCleaning || 0;
            if (sidebarNeedsCleaning) sidebarNeedsCleaning.textContent = data.needsCleaning || 0;
            if (sidebarMaintenance) sidebarMaintenance.textContent = data.maintenance || 0;
            if (sidebarCheckout) sidebarCheckout.textContent = data.checkedOut || 0;

            // Highlight count if greater than 0
            if (data.needsCleaning > 0) {
                if (needsCleaningBadge) {
                    needsCleaningBadge.classList.remove('bg-warning');
                    needsCleaningBadge.classList.add('bg-danger');
                }
                if (sidebarNeedsCleaning) {
                    sidebarNeedsCleaning.classList.remove('bg-warning');
                    sidebarNeedsCleaning.classList.add('bg-danger');
                }
            }
        },
        error: function () {
            console.log('Error fetching room status counts');
        }
    });
}

// Initialize Cleaning Performance Chart
function initCleaningPerformanceChart() {
    const chartCanvas = document.getElementById('cleaningPerformanceChart');
    if (!chartCanvas) return; // Exit if not on admin housekeeping dashboard

    var ctx = chartCanvas.getContext('2d');
    var cleaningPerformanceChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
            datasets: [{
                label: "Completed",
                lineTension: 0.3,
                backgroundColor: "rgba(40, 167, 69, 0.05)",
                borderColor: "#28a745",
                pointRadius: 3,
                pointBackgroundColor: "#28a745",
                pointBorderColor: "#28a745",
                pointHoverRadius: 5,
                pointHoverBackgroundColor: "#28a745",
                pointHoverBorderColor: "#28a745",
                pointHitRadius: 10,
                pointBorderWidth: 2,
                data: [120, 115, 130, 125, 140, 145, 135, 150, 155, 160, 150, 145],
            },
            {
                label: "Assigned",
                lineTension: 0.3,
                backgroundColor: "rgba(255, 193, 7, 0.05)",
                borderColor: "#ffc107",
                pointRadius: 3,
                pointBackgroundColor: "#ffc107",
                pointBorderColor: "#ffc107",
                pointHoverRadius: 5,
                pointHoverBackgroundColor: "#ffc107",
                pointHoverBorderColor: "#ffc107",
                pointHitRadius: 10,
                pointBorderWidth: 2,
                data: [130, 125, 135, 130, 145, 150, 140, 155, 160, 165, 155, 150],
            },
            {
                label: "Overdue",
                lineTension: 0.3,
                backgroundColor: "rgba(220, 53, 69, 0.05)",
                borderColor: "#dc3545",
                pointRadius: 3,
                pointBackgroundColor: "#dc3545",
                pointBorderColor: "#dc3545",
                pointHoverRadius: 5,
                pointHoverBackgroundColor: "#dc3545",
                pointHoverBorderColor: "#dc3545",
                pointHitRadius: 10,
                pointBorderWidth: 2,
                data: [10, 8, 5, 5, 5, 6, 8, 6, 7, 5, 8, 9],
            }]
        },
        options: {
            maintainAspectRatio: false,
            layout: {
                padding: {
                    left: 10,
                    right: 25,
                    top: 25,
                    bottom: 0
                }
            },
            plugins: {
                legend: {
                    display: false
                }
            },
            scales: {
                y: {
                    ticks: {
                        maxTicksLimit: 5,
                        padding: 10
                    },
                    grid: {
                        color: "rgb(234, 236, 244)",
                        drawBorder: false,
                        borderDash: [2],
                        zeroLineBorderDash: [2]
                    }
                },
                x: {
                    ticks: {
                        maxTicksLimit: 7,
                        padding: 10
                    },
                    grid: {
                        display: false,
                        drawBorder: false
                    }
                }
            }
        }
    });
}

// Initialize all housekeeping functionality
document.addEventListener('DOMContentLoaded', function () {
    initRoomFiltering();
    initCleaningChecklist();
    fetchRoomStatusCounts();
    initCleaningPerformanceChart();
});


