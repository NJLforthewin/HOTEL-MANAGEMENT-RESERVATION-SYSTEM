document.addEventListener('DOMContentLoaded', function () {
    const roomSelect = document.getElementById('RoomId');
    const checkInInput = document.getElementById('CheckInDate');
    const checkOutInput = document.getElementById('CheckOutDate');
    const totalOutput = document.getElementById('estimatedTotal');

    const today = new Date().toISOString().split('T')[0];
    checkInInput.min = today;

    checkInInput.addEventListener('change', function () {
        const nextDay = new Date(this.value);
        nextDay.setDate(nextDay.getDate() + 1);
        checkOutInput.min = nextDay.toISOString().split('T')[0];

        if (checkOutInput.value && new Date(checkOutInput.value) <= new Date(this.value)) {
            checkOutInput.value = nextDay.toISOString().split('T')[0];
        }

        calculateTotal();
    });

    checkOutInput.addEventListener('change', calculateTotal);
    roomSelect.addEventListener('change', calculateTotal);

    function calculateTotal() {
        if (!roomSelect.value || !checkInInput.value || !checkOutInput.value) {
            totalOutput.value = "$0.00";
            return;
        }

        const selectedOption = roomSelect.options[roomSelect.selectedIndex];
        const pricePerNight = parseFloat(selectedOption.dataset.price);

        const checkIn = new Date(checkInInput.value);
        const checkOut = new Date(checkOutInput.value);

        const nights = Math.round((checkOut - checkIn) / (1000 * 60 * 60 * 24));

        if (nights > 0 && !isNaN(pricePerNight)) {
            const total = nights * pricePerNight;
            totalOutput.value = `$${total.toFixed(2)} (${nights} night${nights > 1 ? 's' : ''})`;
        } else {
            totalOutput.value = "$0.00";
        }
    }

    calculateTotal();
});