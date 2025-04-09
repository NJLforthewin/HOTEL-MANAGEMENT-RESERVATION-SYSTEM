/**
 * BookingLayout.js - JavaScript functionality for the booking layout
 */

// Modern approach using DOMContentLoaded instead of jQuery ready
document.addEventListener('DOMContentLoaded', function () {
    // Mobile menu toggle functionality
    document.querySelectorAll('.menu-toggle').forEach(function (element) {
        element.addEventListener('click', function () {
            document.querySelector('.frontdesk-sidebar').classList.toggle('show');
        });
    });

    // Close sidebar when clicking outside on mobile
    document.addEventListener('click', function (e) {
        if (window.innerWidth <= 768) {
            const sidebar = document.querySelector('.frontdesk-sidebar');
            const menuToggle = document.querySelector('.menu-toggle');

            if (sidebar && menuToggle) {
                if (!sidebar.contains(e.target) && !menuToggle.contains(e.target)) {
                    sidebar.classList.remove('show');
                }
            }
        }
    });

    // Add mobile menu toggle button dynamically for screens <= 768px
    if (document.querySelectorAll('.menu-toggle').length === 0) {
        const logoContainer = document.querySelector('.logo-container');
        if (logoContainer) {
            const menuToggle = document.createElement('div');
            menuToggle.className = 'menu-toggle';
            menuToggle.innerHTML = '<i class="fas fa-bars"></i>';
            logoContainer.parentNode.insertBefore(menuToggle, logoContainer);
        }
    }

    // Active link highlighting
    const currentPath = window.location.pathname;
    document.querySelectorAll('.sidebar-link').forEach(function (link) {
        const linkPath = link.getAttribute('href');
        if (linkPath && currentPath.includes(linkPath) && linkPath !== '/') {
            link.classList.add('active');
        }
    });

    // Handle modal functionality for the Dashboard page
    if (currentPath.includes('FrontDesk/Dashboard')) {
        initializeDashboardModals();
    }
});

/**
 * Initialize modals and functionality specific to the Dashboard page
 */
function initializeDashboardModals() {
    // Initialize search guest modal
    const searchBtn = document.getElementById('searchBtn');
    if (searchBtn) {
        searchBtn.addEventListener('click', function (e) {
            e.preventDefault();
            const modal = new bootstrap.Modal(document.getElementById('searchGuestModal'));
            modal.show();
        });
    }

    // Initialize checkout modal
    const checkoutBtn = document.getElementById('checkoutBtn');
    if (checkoutBtn) {
        checkoutBtn.addEventListener('click', function (e) {
            e.preventDefault();
            const modal = new bootstrap.Modal(document.getElementById('checkoutModal'));
            modal.show();
        });
    }

    // Guest search functionality
    const guestSearch = document.getElementById('guestSearch');
    if (guestSearch) {
        guestSearch.addEventListener('input', function () {
            const searchTerm = this.value.trim();
            if (searchTerm.length > 2) {
                searchGuests(searchTerm);
            } else {
                document.getElementById('searchResults').innerHTML = '';
            }
        });
    }

    // Room checkout functionality
    const roomNumber = document.getElementById('roomNumber');
    if (roomNumber) {
        roomNumber.addEventListener('input', function () {
            const roomNum = this.value.trim();
            if (roomNum.length > 0) {
                fetchRoomDetails(roomNum);
            } else {
                document.getElementById('checkoutDetails').innerHTML = '';
            }
        });
    }

    // Process checkout submit handler
    const processCheckoutBtn = document.getElementById('processCheckoutBtn');
    if (processCheckoutBtn) {
        processCheckoutBtn.addEventListener('click', function () {
            const roomNum = document.getElementById('roomNumber').value.trim();
            if (roomNum.length > 0) {
                processCheckout(roomNum);
            }
        });
    }
}

/**
 * Search for guests by name, email or phone
 * @param {string} searchTerm The search term
 */
function searchGuests(searchTerm) {
    // Show loading state
    const searchResults = document.getElementById('searchResults');
    searchResults.innerHTML = '<div class="text-center"><i class="fas fa-spinner fa-spin"></i> Searching...</div>';

    // Use fetch API instead of jQuery AJAX
    fetch(`/FrontDesk/SearchGuests?term=${encodeURIComponent(searchTerm)}`)
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            // Handle successful response
            if (data.length > 0) {
                displaySearchResults(data);
            } else {
                searchResults.innerHTML = '<div class="alert alert-info">No guests found matching your search.</div>';
            }
        })
        .catch(error => {
            // Handle error
            searchResults.innerHTML = '<div class="alert alert-danger">Error occurred while searching. Please try again.</div>';
            console.error('Error:', error);
        });
}

/**
 * Display search results in the modal
 * @param {Array} guests Array of guest data
 */
function displaySearchResults(guests) {
    let html = '<div class="list-group">';

    guests.forEach(function (guest) {
        html += `
            <a href="/FrontDesk/GuestDetails/${guest.guestId}" class="list-group-item list-group-item-action">
                <div class="d-flex justify-content-between align-items-center">
                    <h6 class="mb-1">${guest.name}</h6>
                    <span class="badge ${guest.status === 'CheckedIn' ? 'bg-success' : 'bg-warning'}">${guest.status}</span>
                </div>
                <p class="mb-1">${guest.email || 'No email'}</p>
                <small>${guest.phone || 'No phone'}</small>
            </a>
        `;
    });

    html += '</div>';
    document.getElementById('searchResults').innerHTML = html;
}

/**
 * Fetch room details for checkout
 * @param {string} roomNumber The room number
 */
function fetchRoomDetails(roomNumber) {
    // Show loading state
    const checkoutDetails = document.getElementById('checkoutDetails');
    checkoutDetails.innerHTML = '<div class="text-center"><i class="fas fa-spinner fa-spin"></i> Loading room details...</div>';

    // Use fetch API instead of jQuery AJAX
    fetch(`/FrontDesk/GetRoomDetails?roomNumber=${encodeURIComponent(roomNumber)}`)
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            if (data && data.bookingId) {
                // Room has active booking
                displayCheckoutDetails(data);
            } else {
                // No active booking found
                checkoutDetails.innerHTML = '<div class="alert alert-warning">No active booking found for this room.</div>';
            }
        })
        .catch(error => {
            // Handle error
            checkoutDetails.innerHTML = '<div class="alert alert-danger">Error loading room details. Please try again.</div>';
            console.error('Error:', error);
        });
}

/**
 * Display checkout details in the modal
 * @param {Object} data Room and booking details
 */
function displayCheckoutDetails(data) {
    const html = `
        <div class="card">
            <div class="card-body">
                <h5 class="card-title">Room ${data.roomNumber} - ${data.roomType}</h5>
                <div class="card-text">
                    <p><strong>Guest:</strong> ${data.guestName}</p>
                    <p><strong>Check-In:</strong> ${data.checkInDate}</p>
                    <p><strong>Scheduled Check-Out:</strong> ${data.checkOutDate}</p>
                    <p><strong>Total Amount:</strong> $${data.totalAmount.toFixed(2)}</p>
                </div>
                <form id="checkoutForm" action="/FrontDesk/ProcessCheckout" method="post">
                    <input type="hidden" name="roomNumber" value="${data.roomNumber}" />
                    <button type="submit" class="btn btn-danger mt-2 w-100">
                        <i class="fas fa-sign-out-alt me-1"></i> Process Checkout
                    </button>
                </form>
            </div>
        </div>
    `;

    document.getElementById('checkoutDetails').innerHTML = html;
}

/**
 * Process room checkout
 * @param {string} roomNumber The room number
 */
function processCheckout(roomNumber) {
    // Use fetch API for POST request
    fetch('/FrontDesk/ProcessCheckout', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'X-Requested-With': 'XMLHttpRequest'
        },
        body: `roomNumber=${encodeURIComponent(roomNumber)}`
    })
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(response => {
            if (response.success) {
                // Checkout successful
                const checkoutModal = document.getElementById('checkoutModal');
                if (checkoutModal) {
                    const modal = bootstrap.Modal.getInstance(checkoutModal);
                    if (modal) modal.hide();
                }

                showToast('success', 'Checkout successful', response.message);

                // Reload page after a short delay
                setTimeout(function () {
                    window.location.reload();
                }, 1500);
            } else {
                // Checkout failed
                showToast('error', 'Checkout failed', response.message);
            }
        })
        .catch(error => {
            // Handle error
            showToast('error', 'Error', 'An error occurred during checkout process.');
            console.error('Error:', error);
        });
}

/**
 * Show toast notification
 * @param {string} type Type of toast (success, error, warning, info)
 * @param {string} title Toast title
 * @param {string} message Toast message
 */
function showToast(type, title, message) {
    // Check if toast container exists, if not create it
    let toastContainer = document.getElementById('toast-container');
    if (!toastContainer) {
        toastContainer = document.createElement('div');
        toastContainer.id = 'toast-container';
        toastContainer.className = 'position-fixed top-0 end-0 p-3';
        toastContainer.style.zIndex = '1100';
        document.body.appendChild(toastContainer);
    }

    // Create toast element
    const toastId = 'toast-' + Date.now();
    const bgClass = type === 'success' ? 'bg-success' :
        type === 'error' ? 'bg-danger' :
            type === 'warning' ? 'bg-warning' : 'bg-info';

    const toastHtml = `
        <div id="${toastId}" class="toast" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="toast-header ${bgClass} text-white">
                <strong class="me-auto">${title}</strong>
                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
            <div class="toast-body">
                ${message}
            </div>
        </div>
    `;

    // Add toast to container
    toastContainer.insertAdjacentHTML('beforeend', toastHtml);

    // Initialize and show toast
    const toastElement = document.getElementById(toastId);
    if (toastElement) {
        const toast = new bootstrap.Toast(toastElement, { autohide: true, delay: 5000 });
        toast.show();

        // Remove toast from DOM after it's hidden
        toastElement.addEventListener('hidden.bs.toast', function () {
            this.remove();
        });
    }
}