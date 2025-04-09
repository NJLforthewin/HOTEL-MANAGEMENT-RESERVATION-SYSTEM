// housekeepingChart.js
function initializeCleaningPerformanceChart(chartLabels, completedData, assignedData, pendingData) {
    var ctx = document.getElementById('cleaningPerformanceChart').getContext('2d');

    var myChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: chartLabels,
            datasets: [
                {
                    label: 'Completed',
                    data: completedData,
                    borderColor: '#1cc88a',
                    backgroundColor: 'rgba(28, 200, 138, 0.1)',
                    borderWidth: 2,
                    pointBackgroundColor: '#1cc88a',
                    tension: 0.3
                },
                {
                    label: 'Assigned',
                    data: assignedData,
                    borderColor: '#4e73df',
                    backgroundColor: 'rgba(78, 115, 223, 0.1)',
                    borderWidth: 2,
                    pointBackgroundColor: '#4e73df',
                    tension: 0.3
                },
                {
                    label: 'Pending',
                    data: pendingData,
                    borderColor: '#f6c23e',
                    backgroundColor: 'rgba(246, 194, 62, 0.1)',
                    borderWidth: 2,
                    pointBackgroundColor: '#f6c23e',
                    tension: 0.3
                }
            ]
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
            scales: {
                x: {
                    grid: {
                        display: false,
                        drawBorder: false
                    }
                },
                y: {
                    beginAtZero: true,
                    grid: {
                        color: "rgb(234, 236, 244)",
                        zeroLineColor: "rgb(234, 236, 244)",
                        drawBorder: false,
                        borderDash: [2],
                        zeroLineBorderDash: [2]
                    }
                }
            },
            plugins: {
                legend: {
                    display: false
                }
            }
        }
    });
}