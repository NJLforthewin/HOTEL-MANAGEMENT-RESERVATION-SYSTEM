document.addEventListener('DOMContentLoaded', function () {
    var pendingTab = document.getElementById('pending-tab');
    var confirmedTab = document.getElementById('confirmed-tab');

    if (pendingTab && confirmedTab) {
        pendingTab.addEventListener('click', function (e) {
            e.preventDefault();

            document.getElementById('confirmed').classList.remove('show', 'active');
            document.getElementById('pending').classList.add('show', 'active');
            confirmedTab.classList.remove('active');
            pendingTab.classList.add('active');
        });

        confirmedTab.addEventListener('click', function (e) {
            e.preventDefault();
            document.getElementById('pending').classList.remove('show', 'active');
            document.getElementById('confirmed').classList.add('show', 'active');

            pendingTab.classList.remove('active');
            confirmedTab.classList.add('active');
        });
    }
});