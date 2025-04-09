document.addEventListener('DOMContentLoaded', function () {
    const reportType = document.getElementById('reportTypePage').dataset.reportType;

    if (reportType === 'occupancy') {
        initOccupancyCharts();
    } else if (reportType === 'revenue') {
        initRevenueCharts();
    } else if (reportType === 'bookings') {
        initBookingsCharts();
    } else if (reportType === 'guests') {
        initGuestsCharts();
    }
});

function initOccupancyCharts() {
    const categoryCtx = document.getElementById('categoryOccupancyChart');
    if (categoryCtx) {
        const categoryData = JSON.parse(categoryCtx.dataset.chartData || '[]');
        new Chart(categoryCtx, {
            type: 'bar',
            data: {
                labels: categoryData.map(function (item) { return item.Category; }),
                datasets: [{
                    label: 'Total Rooms',
                    data: categoryData.map(function (item) { return item.TotalRooms; }),
                    backgroundColor: '#61122f80',
                    borderColor: '#61122f',
                    borderWidth: 1
                }, {
                    label: 'Booked Rooms',
                    data: categoryData.map(function (item) { return item.BookedRooms; }),
                    backgroundColor: '#e0707080',
                    borderColor: '#e07070',
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true,
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        });
    }

    const trendCtx = document.getElementById('occupancyTrendChart');
    if (trendCtx) {
        const trendData = JSON.parse(trendCtx.dataset.chartData || '[]');
        new Chart(trendCtx, {
            type: 'line',
            data: {
                labels: trendData.map(function (item) { return item.Date; }),
                datasets: [{
                    label: 'Occupancy Rate %',
                    data: trendData.map(function (item) { return item.OccupancyRate; }),
                    backgroundColor: '#61122f',
                    borderColor: '#61122f',
                    borderWidth: 2,
                    tension: 0.1
                }]
            },
            options: {
                responsive: true,
                scales: {
                    y: {
                        beginAtZero: true,
                        max: 100
                    }
                }
            }
        });
    }
}

function initRevenueCharts() {
    const paymentCtx = document.getElementById('paymentChart');
    if (paymentCtx) {
        const paymentData = JSON.parse(paymentCtx.dataset.chartData || '[]');
        new Chart(paymentCtx, {
            type: 'pie',
            data: {
                labels: paymentData.map(function (item) { return item.Method; }),
                datasets: [{
                    data: paymentData.map(function (item) { return item.Amount; }),
                    backgroundColor: [
                        '#61122f', '#8c1c42', '#b72554', '#e03466', '#e86487'
                    ],
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true
            }
        });
    }
    const categoryCtx = document.getElementById('categoryRevenueChart');
    if (categoryCtx) {
        const categoryData = JSON.parse(categoryCtx.dataset.chartData || '[]');
        new Chart(categoryCtx, {
            type: 'doughnut',
            data: {
                labels: categoryData.map(function (item) { return item.Category; }),
                datasets: [{
                    data: categoryData.map(function (item) { return item.Amount; }),
                    backgroundColor: [
                        '#61122f', '#8c1c42', '#b72554', '#e03466', '#e86487'
                    ],
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true
            }
        });
    }

    const trendCtx = document.getElementById('revenueTrendChart');
    if (trendCtx) {
        const trendData = JSON.parse(trendCtx.dataset.chartData || '[]');
        new Chart(trendCtx, {
            type: 'line',
            data: {
                labels: trendData.map(function (item) { return item.Date; }),
                datasets: [{
                    label: 'Daily Revenue ($)',
                    data: trendData.map(function (item) { return item.Amount; }),
                    backgroundColor: '#61122f',
                    borderColor: '#61122f',
                    borderWidth: 2,
                    tension: 0.1
                }]
            },
            options: {
                responsive: true,
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        });
    }
}

function initBookingsCharts() {
    const statusCtx = document.getElementById('bookingStatusChart');
    if (statusCtx) {
        const statusData = JSON.parse(statusCtx.dataset.chartData || '[]');
        new Chart(statusCtx, {
            type: 'pie',
            data: {
                labels: statusData.map(function (item) { return item.Status; }),
                datasets: [{
                    data: statusData.map(function (item) { return item.Count; }),
                    backgroundColor: [
                        '#61122f', '#8c1c42', '#b72554', '#e03466', '#e86487'
                    ],
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true
            }
        });
    }

    const paymentCtx = document.getElementById('bookingPaymentChart');
    if (paymentCtx) {
        const paymentData = JSON.parse(paymentCtx.dataset.chartData || '[]');
        new Chart(paymentCtx, {
            type: 'doughnut',
            data: {
                labels: paymentData.map(function (item) { return item.Status; }),
                datasets: [{
                    data: paymentData.map(function (item) { return item.Count; }),
                    backgroundColor: [
                        '#61122f', '#8c1c42', '#b72554', '#e03466', '#e86487'
                    ],
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true
            }
        });
    }

    const dayCtx = document.getElementById('bookingDayChart');
    if (dayCtx) {
        const dayData = JSON.parse(dayCtx.dataset.chartData || '[]');
        new Chart(dayCtx, {
            type: 'bar',
            data: {
                labels: dayData.map(function (item) { return item.Day; }),
                datasets: [{
                    label: 'Bookings',
                    data: dayData.map(function (item) { return item.Count; }),
                    backgroundColor: '#61122f80',
                    borderColor: '#61122f',
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true,
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        });
    }

    const trendCtx = document.getElementById('bookingTrendChart');
    if (trendCtx) {
        const trendData = JSON.parse(trendCtx.dataset.chartData || '[]');
        new Chart(trendCtx, {
            type: 'line',
            data: {
                labels: trendData.map(function (item) { return item.Date; }),
                datasets: [{
                    label: 'Bookings',
                    data: trendData.map(function (item) { return item.Count; }),
                    backgroundColor: '#61122f',
                    borderColor: '#61122f',
                    borderWidth: 2,
                    tension: 0.1
                }]
            },
            options: {
                responsive: true,
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        });
    }
}

function initGuestsCharts() {
    const typeCtx = document.getElementById('guestTypeChart');
    if (typeCtx) {
        const newGuests = parseInt(typeCtx.dataset.newGuests || 0);
        const returningGuests = parseInt(typeCtx.dataset.returningGuests || 0);

        new Chart(typeCtx, {
            type: 'pie',
            data: {
                labels: ['New Guests', 'Returning Guests'],
                datasets: [{
                    data: [newGuests, returningGuests],
                    backgroundColor: [
                        '#61122f', '#8c1c42'
                    ],
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true
            }
        });
    }
}