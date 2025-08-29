// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Modern Toast Notification System
class ToastNotification {
    constructor() {
        this.createToastContainer();
        this.toastQueue = [];
        this.isProcessing = false;
    }

    createToastContainer() {
        // Remove existing container if any
        const existingContainer = document.getElementById('toast-container');
        if (existingContainer) {
            existingContainer.remove();
        }

        // Create new container
        this.container = document.createElement('div');
        this.container.id = 'toast-container';
        this.container.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            z-index: 9999;
            display: flex;
            flex-direction: column;
            gap: 12px;
            max-width: 420px;
            pointer-events: none;
        `;
        document.body.appendChild(this.container);
    }

    show(message, type = 'success', duration = 5000, persistent = false) {
        const toast = this.createToastElement(message, type);
        this.container.appendChild(toast);

        // Animate in
        setTimeout(() => {
            toast.style.transform = 'translateX(0)';
            toast.style.opacity = '1';
        }, 10);

        // Auto remove after duration only if not persistent
        if (!persistent) {
            setTimeout(() => {
                this.removeToast(toast);
            }, duration);
        }

        return toast;
    }

    createToastElement(message, type) {
        const toast = document.createElement('div');
        toast.className = 'toast-notification';
        
        const { title, icon, leftBarColor, iconBgColor } = this.getToastConfig(type);
        
        toast.style.cssText = `
            background: white;
            color: #1f2937;
            padding: 0;
            border-radius: 12px;
            box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
            display: flex;
            align-items: stretch;
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            transform: translateX(100%);
            opacity: 0;
            transition: all 0.3s ease;
            pointer-events: auto;
            cursor: pointer;
            max-width: 100%;
            word-wrap: break-word;
            overflow: hidden;
        `;

        toast.innerHTML = `
            <div class="toast-left-bar" style="
                width: 6px;
                background: ${leftBarColor};
                flex-shrink: 0;
            "></div>
            <div class="toast-content" style="
                flex: 1;
                padding: 16px 20px;
                display: flex;
                align-items: flex-start;
                gap: 12px;
                position: relative;
            ">
                <div class="toast-icon" style="
                    width: 24px;
                    height: 24px;
                    border-radius: 50%;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    flex-shrink: 0;
                    background: ${iconBgColor};
                    color: white;
                    font-size: 12px;
                    font-weight: bold;
                ">
                    ${icon}
                </div>
                <div class="toast-text" style="flex: 1; min-width: 0;">
                    <div class="toast-title" style="
                        font-weight: 700;
                        font-size: 14px;
                        margin-bottom: 4px;
                        color: #1f2937;
                    ">${title}</div>
                    <div class="toast-message" style="
                        font-size: 13px;
                        line-height: 1.4;
                        color: #6b7280;
                    ">${message}</div>
                </div>
                <button class="toast-close" style="
                    background: none;
                    border: none;
                    color: #9ca3af;
                    cursor: pointer;
                    padding: 4px;
                    width: 20px;
                    height: 20px;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    opacity: 0.7;
                    transition: all 0.2s ease;
                    flex-shrink: 0;
                    font-size: 16px;
                    font-weight: bold;
                    border-radius: 4px;
                " onclick="this.parentElement.parentElement.remove()">
                    ×
                </button>
            </div>
        `;

        // Add hover effects
        const closeBtn = toast.querySelector('.toast-close');
        closeBtn.addEventListener('mouseenter', () => {
            closeBtn.style.opacity = '1';
            closeBtn.style.color = '#6b7280';
            closeBtn.style.backgroundColor = '#f3f4f6';
        });
        closeBtn.addEventListener('mouseleave', () => {
            closeBtn.style.opacity = '0.7';
            closeBtn.style.color = '#9ca3af';
            closeBtn.style.backgroundColor = 'transparent';
        });

        // Click to dismiss
        toast.addEventListener('click', (e) => {
            if (e.target !== closeBtn && !closeBtn.contains(e.target)) {
                this.removeToast(toast);
            }
        });

        return toast;
    }

    getToastConfig(type) {
        switch (type) {
            case 'success':
                return {
                    title: 'Success',
                    icon: '✓',
                    leftBarColor: '#10b981',
                    iconBgColor: '#10b981'
                };
            case 'error':
                return {
                    title: 'Error',
                    icon: '✕',
                    leftBarColor: '#ef4444',
                    iconBgColor: '#ef4444'
                };
            case 'warning':
                return {
                    title: 'Warning',
                    icon: '!',
                    leftBarColor: '#f59e0b',
                    iconBgColor: '#f59e0b'
                };
            case 'info':
                return {
                    title: 'Info',
                    icon: 'i',
                    leftBarColor: '#3b82f6',
                    iconBgColor: '#3b82f6'
                };
            default:
                return {
                    title: 'Success',
                    icon: '✓',
                    leftBarColor: '#10b981',
                    iconBgColor: '#10b981'
                };
        }
    }

    removeToast(toast) {
        if (toast && toast.parentNode) {
            toast.style.transform = 'translateX(100%)';
            toast.style.opacity = '0';
            setTimeout(() => {
                if (toast.parentNode) {
                    toast.remove();
                }
            }, 300);
        }
    }

    clearAll() {
        const toasts = this.container.querySelectorAll('.toast-notification');
        toasts.forEach(toast => this.removeToast(toast));
    }
}

// Global toast instance
let toastNotification;

// Initialize toast system when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    toastNotification = new ToastNotification();
    
    // Handle server-side messages from TempData
    handleServerMessages();
});

// Function to show toast (global access)
function showToast(message, type = 'success', duration = 5000, persistent = false) {
    if (toastNotification) {
        return toastNotification.show(message, type, duration, persistent);
    }
}

// Function to clear all toasts
function clearAllToasts() {
    if (toastNotification) {
        toastNotification.clearAll();
    }
}

// Handle server-side messages from TempData
function handleServerMessages() {
    // Check for TempData messages in the page
    const successMessage = document.querySelector('[data-tempdata="success"]');
    const errorMessage = document.querySelector('[data-tempdata="error"]');
    const alertMessage = document.querySelector('[data-tempdata="alert"]');
    const infoMessage = document.querySelector('[data-tempdata="info"]');
    const warningMessage = document.querySelector('[data-tempdata="warning"]');
    const loginSuccessMessage = document.querySelector('[data-tempdata="login-success"]');
    const submissionSuccessMessage = document.querySelector('[data-tempdata="submission-success"]');

    if (successMessage) {
        showToast(successMessage.textContent, 'success');
        successMessage.remove();
    }

    if (errorMessage) {
        showToast(errorMessage.textContent, 'error');
        errorMessage.remove();
    }

    if (alertMessage) {
        showToast(alertMessage.textContent, 'error');
        alertMessage.remove();
    }

    if (infoMessage) {
        showToast(infoMessage.textContent, 'info');
        infoMessage.remove();
    }

    if (warningMessage) {
        showToast(warningMessage.textContent, 'warning');
        warningMessage.remove();
    }

    if (loginSuccessMessage) {
        showToast(loginSuccessMessage.textContent, 'success');
        loginSuccessMessage.remove();
    }

    if (submissionSuccessMessage) {
        showSubmissionToast(submissionSuccessMessage.textContent, 'success');
        submissionSuccessMessage.remove();
    }
}

// Enhanced alert replacement
function showAlert(message, type = 'info') {
    showToast(message, type);
}

// Success message helper
function showSuccess(message) {
    showToast(message, 'success');
}

// Error message helper
function showError(message) {
    showToast(message, 'error');
}

// Warning message helper
function showWarning(message) {
    showToast(message, 'warning');
}

// Info message helper
function showInfo(message) {
    showToast(message, 'info');
}

// Persistent toast helpers for login and submission actions
function showLoginToast(message, type = 'success') {
    showToast(message, type, 0, true); // Persistent toast for login
}

function showSubmissionToast(message, type = 'success') {
    showToast(message, type, 0, true); // Persistent toast for submissions
}

// Replace native alert with toast
window.alert = function(message) {
    showToast(message, 'info');
};

// Replace native confirm with custom implementation (optional)
// window.confirm = function(message) {
//     // For now, keep native confirm but you can implement a custom one
//     return confirm(message);
// };

// Login page enhancements
document.addEventListener('DOMContentLoaded', function() {
    // Login form enhancements
    const loginForm = document.querySelector('.login-form');
    const loginBtn = document.querySelector('.login-btn');
    
    if (loginForm && loginBtn) {
        // Add loading state to button on form submit
        loginForm.addEventListener('submit', function() {
            loginBtn.classList.add('loading');
            loginBtn.querySelector('.btn-text').textContent = 'Signing In...';
        });
        
        // Add focus effects to form inputs
        const inputs = loginForm.querySelectorAll('.form-control');
        inputs.forEach(input => {
            input.addEventListener('focus', function() {
                this.parentElement.classList.add('focused');
            });
            
            input.addEventListener('blur', function() {
                if (!this.value) {
                    this.parentElement.classList.remove('focused');
                }
            });
            
            // Add floating label effect
            if (this.value) {
                this.parentElement.classList.add('focused');
            }
        });
        
        // Add ripple effect to button
        loginBtn.addEventListener('click', function(e) {
            const ripple = document.createElement('span');
            const rect = this.getBoundingClientRect();
            const size = Math.max(rect.width, rect.height);
            const x = e.clientX - rect.left - size / 2;
            const y = e.clientY - rect.top - size / 2;
            
            ripple.style.width = ripple.style.height = size + 'px';
            ripple.style.left = x + 'px';
            ripple.style.top = y + 'px';
            ripple.classList.add('ripple');
            
            this.appendChild(ripple);
            
            setTimeout(() => {
                ripple.remove();
            }, 600);
        });
    }
    
    // Add smooth scroll behavior
    document.documentElement.style.scrollBehavior = 'smooth';
    
    // Add keyboard navigation support
    document.addEventListener('keydown', function(e) {
        if (e.key === 'Enter' && document.activeElement.classList.contains('form-control')) {
            const form = document.querySelector('.login-form');
            if (form) {
                form.requestSubmit();
            }
        }
    });
});

// Add ripple effect styles
const style = document.createElement('style');
style.textContent = `
    .login-btn {
        position: relative;
        overflow: hidden;
    }
    
    .ripple {
        position: absolute;
        border-radius: 50%;
        background: rgba(255, 255, 255, 0.3);
        transform: scale(0);
        animation: ripple-animation 0.6s linear;
        pointer-events: none;
    }
    
    @keyframes ripple-animation {
        to {
            transform: scale(4);
            opacity: 0;
        }
    }
    
    .form-group.focused .form-label {
        color: #667eea;
        transform: translateY(-2px);
    }
    
    .form-group.focused .input-focus-border {
        width: 100%;
    }
`;
document.head.appendChild(style);
