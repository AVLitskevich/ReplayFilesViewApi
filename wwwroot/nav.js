(async function() {
    // Add styles for the navigation header
    const style = document.createElement('style');
    style.textContent = `
        .site-nav {
            position: fixed;
            top: 1rem;
            right: 1rem;
            display: flex;
            gap: 0.75rem;
            z-index: 1000;
        }
        .nav-btn {
            background: rgba(30, 41, 59, 0.7);
            backdrop-filter: blur(8px);
            border: 1px solid rgba(255, 255, 255, 0.1);
            color: #f8fafc;
            padding: 0.5rem 1rem;
            border-radius: 8px;
            font-size: 0.875rem;
            font-weight: 500;
            text-decoration: none;
            transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
            cursor: pointer;
            box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);
        }
        .nav-btn:hover {
            background: rgba(51, 65, 85, 0.9);
            transform: translateY(-2px);
            border-color: rgba(99, 102, 241, 0.5);
            box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.2);
        }
        .nav-btn-primary {
            background: rgba(99, 102, 241, 0.8);
            border-color: rgba(99, 102, 241, 0.3);
        }
        .nav-btn-primary:hover {
            background: rgba(79, 70, 229, 0.9);
        }
    `;
    document.head.appendChild(style);

    // Create the navigation container
    const navContainer = document.createElement('div');
    navContainer.className = 'site-nav';
    document.body.appendChild(navContainer);

    try {
        const response = await fetch('/api/admin/status');
        const data = await response.json();

        if (data.isAuthenticated) {
            // Logged In: Show Admin and Logout
            const adminLink = document.createElement('a');
            adminLink.href = '/admin';
            adminLink.className = 'nav-btn nav-btn-primary';
            adminLink.textContent = 'Admin Dashboard';
            
            const logoutBtn = document.createElement('button');
            logoutBtn.className = 'nav-btn';
            logoutBtn.textContent = 'Sign Out';
            logoutBtn.onclick = async () => {
                await fetch('/api/admin/logout', { method: 'POST' });
                window.location.href = '/';
            };

            navContainer.appendChild(adminLink);
            navContainer.appendChild(logoutBtn);
        } else {
            // Logged Out: Show Login
            const loginLink = document.createElement('a');
            loginLink.href = '/login';
            loginLink.className = 'nav-btn';
            loginLink.textContent = 'Sign In';
            navContainer.appendChild(loginLink);
        }

        // Add Home link if not on index
        if (window.location.pathname !== '/' && window.location.pathname !== '/index.html') {
            const homeLink = document.createElement('a');
            homeLink.href = '/';
            homeLink.className = 'nav-btn';
            homeLink.textContent = 'Home Hub';
            navContainer.prepend(homeLink);
        }

    } catch (error) {
        console.error('Failed to load navigation status:', error);
    }
})();
