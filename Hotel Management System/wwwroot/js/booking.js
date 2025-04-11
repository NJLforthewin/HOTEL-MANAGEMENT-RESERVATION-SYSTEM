// booking.js - Hotel Management System booking functionality

document.addEventListener("DOMContentLoaded", function () {
    // Initialize price display based on selected room
    updatePrice();

    // Update price when room selection changes
    document.getElementById("RoomId").addEventListener("change", function () {
        updatePrice();
    });

    // Update price when dates change
    document.getElementById("CheckInDate").addEventListener("change", updatePrice);
    document.getElementById("CheckOutDate").addEventListener("change", updatePrice);

    // Update page elements when booking type changes
    const bookingTypeRadios = document.querySelectorAll('input[name="BookingType"]');
    bookingTypeRadios.forEach(radio => {
        radio.addEventListener("change", function () {
            const isReservation = document.getElementById("reservationType").checked;

            // Update default dates
            if (isReservation) {
                const tomorrow = new Date();
                tomorrow.setDate(tomorrow.getDate() + 1);
                const dayAfterTomorrow = new Date();
                dayAfterTomorrow.setDate(dayAfterTomorrow.getDate() + 2);

                document.getElementById("CheckInDate").value = formatDate(tomorrow);
                document.getElementById("CheckOutDate").value = formatDate(dayAfterTomorrow);
            } else {
                const today = new Date();
                const tomorrow = new Date();
                tomorrow.setDate(tomorrow.getDate() + 1);

                document.getElementById("CheckInDate").value = formatDate(today);
                document.getElementById("CheckOutDate").value = formatDate(tomorrow);
            }

            // Update price after date change
            updatePrice();
        });
    });

    // Payment method selection
    const paymentMethodRadios = document.querySelectorAll('input[name="PaymentMethod"]');
    paymentMethodRadios.forEach(radio => {
        radio.addEventListener("change", function () {
            // Hide all payment forms
            const paymentForms = document.querySelectorAll('.payment-details-form');
            paymentForms.forEach(form => {
                form.style.display = 'none';
            });

            // Show the selected payment form
            const selectedPayment = this.value;

            if (selectedPayment === 'Credit Card') {
                document.getElementById('creditcard-form').style.display = 'block';
            } else if (selectedPayment === 'GCash') {
                document.getElementById('gcash-form').style.display = 'block';
            } else if (selectedPayment === 'Bank Transfer') {
                document.getElementById('banktransfer-form').style.display = 'block';
            }
        });
    });

    // Form validation before submit
    document.querySelector("form").addEventListener("submit", function (e) {
        const checkIn = new Date(document.getElementById("CheckInDate").value);
        const checkOut = new Date(document.getElementById("CheckOutDate").value);

        if (checkIn >= checkOut) {
            e.preventDefault();
            alert("Check-out date must be after check-in date");
            return false;
        }

        if (!document.getElementById("RoomId").value) {
            e.preventDefault();
            alert("Please select a room");
            return false;
        }

        if (!document.querySelector("input[name='PaymentMethod']:checked")) {
            e.preventDefault();
            alert("Please select a payment method");
            document.querySelector(".payment-error").textContent = "Please select a payment method";
            return false;
        }

        // Make sure we have a numeric total price
        const totalPriceElement = document.getElementById("TotalPrice");
        const totalPrice = parseFloat(totalPriceElement.value.replace(/[^0-9.-]+/g, ""));
        if (isNaN(totalPrice) || totalPrice <= 0) {
            e.preventDefault();
            alert("Invalid total price. Please check your dates and room selection.");
            return false;
        }

        // Set the TotalPrice to the numeric value without currency symbol
        totalPriceElement.value = totalPrice.toFixed(2);

        return true;
    });
});

/**
 * Updates the price display and calculations based on room selection and dates
 */
function updatePrice() {
    // Get the selected room option
    const roomSelect = document.getElementById("RoomId");
    const selectedOption = roomSelect.options[roomSelect.selectedIndex];

    // Get the price from the text of the option - extract the number between ₱ and /night
    const optionText = selectedOption.text;
    const priceMatch = optionText.match(/₱([0-9]+\.?[0-9]*)/);
    let pricePerNight = 0;

    if (priceMatch && priceMatch.length > 1) {
        pricePerNight = parseFloat(priceMatch[1]);
    }

    // Display the price per night
    document.getElementById("PricePerNight").value = "₱" + pricePerNight.toFixed(2);

    // Calculate total price based on dates
    const checkInStr = document.getElementById("CheckInDate").value;
    const checkOutStr = document.getElementById("CheckOutDate").value;

    if (checkInStr && checkOutStr) {
        const checkIn = new Date(checkInStr);
        const checkOut = new Date(checkOutStr);

        if (checkIn && checkOut && checkIn < checkOut) {
            // Calculate the difference in days
            const timeDiff = checkOut.getTime() - checkIn.getTime();
            const days = Math.ceil(timeDiff / (1000 * 3600 * 24));

            // Calculate and display total price
            const totalPrice = days * pricePerNight;
            document.getElementById("TotalPrice").value = totalPrice.toFixed(2);
        } else {
            document.getElementById("TotalPrice").value = "0.00";
        }
    } else {
        document.getElementById("TotalPrice").value = "0.00";
    }
}

/**
 * Helper function to format date as yyyy-mm-dd
 * @param {Date} date - The date to format
 * @returns {string} - Formatted date string
 */
function formatDate(date) {
    const d = new Date(date);
    let month = '' + (d.getMonth() + 1);
    let day = '' + d.getDate();
    const year = d.getFullYear();

    if (month.length < 2) month = '0' + month;
    if (day.length < 2) day = '0' + day;

    return [year, month, day].join('-');
}