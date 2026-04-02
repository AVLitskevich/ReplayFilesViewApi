(async function() {
    // Add styles for the navigation header using the Neon Monolith aesthetic
    const style = document.createElement('style');
    // Ensure fonts are loaded if this script is injected on older unstyled pages
    if (!document.querySelector('link[href*="Space+Grotesk"]')) {
        const link = document.createElement('link');
        link.href = "https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@500;700&display=swap";
        link.rel = "stylesheet";
        document.head.appendChild(link);
    }

    style.textContent = `
        .site-nav {
            position: fixed;
            top: 2rem;
            right: 2.5rem;
            display: flex;
            gap: 1rem;
            z-index: 1000;
            font-family: 'Space Grotesk', sans-serif;
        }
        .nav-btn {
            display: inline-flex;
            align-items: center;
            gap: 0.5rem;
            background: rgba(32, 38, 47, 0.4);
            backdrop-filter: blur(8px);
            border: 1px solid rgba(68, 72, 79, 0.3);
            color: #f1f3fc;
            padding: 0.6rem 1.25rem;
            border-radius: 0.5rem;
            font-size: 0.75rem;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.1em;
            text-decoration: none;
            transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
            cursor: pointer;
        }
        .nav-btn:hover {
            background: rgba(32, 38, 47, 0.9);
            border-color: rgba(68, 72, 79, 0.8);
            transform: translateY(-1px);
        }
        .nav-btn-primary {
            background: rgba(154, 168, 255, 0.1);
            border-color: rgba(154, 168, 255, 0.3);
            color: #9aa8ff;
        }
        .nav-btn-primary:hover {
            background: rgba(154, 168, 255, 0.2);
            border-color: rgba(154, 168, 255, 0.6);
        }
        .nav-btn-error {
            background: rgba(215, 51, 87, 0.1);
            border-color: rgba(215, 51, 87, 0.3);
            color: #d73357;
        }
        .nav-btn-error:hover {
            background: rgba(215, 51, 87, 0.2);
            border-color: rgba(215, 51, 87, 0.6);
        }
        
        @media (max-width: 768px) {
            .site-nav {
                top: 1rem;
                right: 1rem;
            }
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
            if (window.location.pathname !== '/admin' && window.location.pathname !== '/admin.html') {
                const adminLink = document.createElement('a');
                adminLink.href = '/admin.html';
                adminLink.className = 'nav-btn nav-btn-primary';
                adminLink.innerHTML = 'Admin Dashboard';
                navContainer.appendChild(adminLink);
            }
            
            const logoutBtn = document.createElement('button');
            logoutBtn.className = 'nav-btn nav-btn-error';
            logoutBtn.innerHTML = 'Sign Out';
            logoutBtn.onclick = async () => {
                await fetch('/api/admin/logout', { method: 'POST' });
                window.location.href = '/';
            };

            navContainer.appendChild(logoutBtn);
        } else {
            // Logged Out: Show Login
            const loginLink = document.createElement('a');
            loginLink.href = '/login.html';
            loginLink.className = 'nav-btn';
            loginLink.innerHTML = 'Sign In';
            navContainer.appendChild(loginLink);
        }

        // Add Home link if not on index
        if (window.location.pathname !== '/' && window.location.pathname !== '/index.html') {
            const homeLink = document.createElement('a');
            homeLink.href = '/';
            homeLink.className = 'nav-btn';
            homeLink.innerHTML = 'Home Hub';
            navContainer.prepend(homeLink);
        }

    } catch (error) {
        console.error('Failed to load navigation status:', error);
    }
})();
