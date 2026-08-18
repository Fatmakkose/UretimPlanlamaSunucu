
                function openPerformansModal() {
                    var modal = document.getElementById('performansModal');
                    modal.style.display = 'flex';
                    modal.firstElementChild.style.transform = 'scale(0.95)';
                    modal.firstElementChild.style.opacity = '0';
                    setTimeout(() => {
                        modal.firstElementChild.style.transform = 'scale(1)';
                        modal.firstElementChild.style.opacity = '1';
                    }, 10);
                }
                function closePerformansModal() {
                    var modal = document.getElementById('performansModal');
                    modal.firstElementChild.style.transform = 'scale(0.95)';
                    modal.firstElementChild.style.opacity = '0';
                    setTimeout(() => {
                        modal.style.display = 'none';
                    }, 200);
                }

                function closeComparisonModal() {
                    var modal = document.getElementById('kesimComparisonModal');
                    modal.firstElementChild.style.transform = 'scale(0.95)';
                    modal.firstElementChild.style.opacity = '0';
                    setTimeout(() => {
                        modal.style.display = 'none';
                    }, 200);
                }

                window.addEventListener('click', function(e) {
                    var perfModal = document.getElementById('performansModal');
                    if (e.target === perfModal) {
                        closePerformansModal();
                    }
                    var compModal = document.getElementById('kesimComparisonModal');
                    if (e.target === compModal) {
                        closeComparisonModal();
                    }
                });
            
