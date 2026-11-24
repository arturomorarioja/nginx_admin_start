'use strict';

// Keep keyboard navigation visible when using the public site.
document.addEventListener('keydown', (event) => {
    if (event.key === 'Tab') {
        document.documentElement.classList.add('keyboard-navigation');
    }
});
