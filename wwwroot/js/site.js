// Statik web varlÄ±klarÄ±nÄ± paketlemek ve sÄ±kÄ±ÅŸtÄ±rmak iÃ§in bu projeyi yapÄ±landÄ±rma hakkÄ±nda ayrÄ±ntÄ±lar iÃ§in
// lÃ¼tfen https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification adresindeki belgelere bakÄ±n.

// JavaScript kodunuzu buraya yazÄ±n.

/* --- TAB SYSTEM IMPLEMENTATION --- */
var appTabs = {
    tabCounter: 0,
    openTabs: {}, // tabId -> { url, title, icon }

    init: function() {
        if (window.self !== window.top) return; // Do nothing inside iframe

        // Intercept sidebar links
        $('aside.dropdown-sidebar a').on('click', function(e) {
            if ($(this).attr('target') === '_blank') return;
            if ($(this).parent().hasClass('has-submenu')) return; // ignore accordion clicks

            var href = $(this).attr('href');
            if (!href || href === '#' || href.startsWith('javascript:')) return;

            e.preventDefault();
            var title = $(this).find('.nav-text').text().trim() || 'Yeni Sekme';
            var icon = $(this).find('i.menu-icon').attr('class') || 'fa-solid fa-file';

            appTabs.openNewTab(title, href, icon);
        });

        // Push state for main tab initially
        history.replaceState({ tabId: 'main-tab' }, document.title, window.location.pathname + window.location.search);
        
        // Handle browser back/forward buttons
        window.addEventListener('popstate', function(event) {
            if (event.state && event.state.tabId) {
                appTabs.switchTab(event.state.tabId, false);
            }
        });
    },

    openNewTab: function(title, url, icon) {
        if (window.self !== window.top) {
            // Inside iframe: ask parent to open tab
            if (window.parent.appTabs) {
                window.parent.appTabs.openNewTab(title, url, icon);
            } else {
                window.location.href = url;
            }
            return;
        }

        if (url === '/' || url.toLowerCase() === '/home/index' || url.toLowerCase() === '/home') {
            this.switchTab('main-tab');
            return;
        }

        // Check if a tab with this URL already exists
        var existingTabId = null;
        for (var id in this.openTabs) {
            if (this.openTabs[id].url === url || this.openTabs[id].originalUrl === url) {
                existingTabId = id;
                break;
            }
        }

        if (existingTabId) {
            this.switchTab(existingTabId);
            if (this.openTabs[existingTabId].url !== url) {
                var iframe = document.getElementById('iframe-' + existingTabId);
                if (iframe) {
                    iframe.src = url;
                }
            }
            return;
        }

        // Create new tab
        this.tabCounter++;
        var newTabId = 'tab-' + this.tabCounter;

        this.openTabs[newTabId] = {
            url: url,
            originalUrl: url,
            title: title,
            icon: icon
        };

        // Render tab button
        var tabHtml = `
            <div class="tab-item" id="btn-${newTabId}" data-tab-id="${newTabId}" onclick="appTabs.switchTab('${newTabId}')">
                <i class="${icon} tab-icon"></i>
                <span class="tab-title" title="${title}">${title}</span>
                <div class="tab-close" onclick="appTabs.closeTab('${newTabId}', event)">
                    <i class="fa-solid fa-xmark"></i>
                </div>
            </div>
        `;
        $('#tab-list').append(tabHtml);

        // Render tab content (iframe)
        var iframeHtml = `
            <div class="tab-pane" id="pane-${newTabId}">
                <iframe src="${url}" class="tab-iframe" id="iframe-${newTabId}" title="${title}" onload="appTabs.onIframeLoad('${newTabId}')"></iframe>
            </div>
        `;
        $('#tab-system-content').append(iframeHtml);

        this.switchTab(newTabId);
    },

    switchTab: function(tabId, pushState = true) {
        // Deactivate all
        $('.tab-item').removeClass('active');
        $('.tab-pane').removeClass('active');

        // Activate target
        if (tabId === 'main-tab') {
            $('.tab-item[data-tab-id="main-tab"]').addClass('active');
            $('#pane-main-tab').addClass('active');
            if (pushState) history.pushState({ tabId: 'main-tab' }, document.title, window.location.pathname + window.location.search);
        } else {
            $('#btn-' + tabId).addClass('active');
            $('#pane-' + tabId).addClass('active');
            if (pushState) history.pushState({ tabId: tabId }, this.openTabs[tabId].title, this.openTabs[tabId].url);
        }

        // Scroll tab list to ensure active tab is visible
        var tabBtn = document.getElementById(tabId === 'main-tab' ? 'btn-main-tab' : 'btn-' + tabId);
        if (tabBtn) tabBtn.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
    },

    closeTab: function(tabId, event) {
        if (event) event.stopPropagation();

        var isActive = $('#btn-' + tabId).hasClass('active');
        
        var prevTabBtn = $('#btn-' + tabId).prev('.tab-item');
        var nextTabToSwitch = 'main-tab';
        if (prevTabBtn.length > 0) {
            nextTabToSwitch = prevTabBtn.attr('data-tab-id') || 'main-tab';
        }

        // Remove elements
        $('#btn-' + tabId).remove();
        $('#pane-' + tabId).remove();
        delete this.openTabs[tabId];

        // If it was active, switch to previous tab
        if (isActive) {
            this.switchTab(nextTabToSwitch);
        }
    },

    onIframeLoad: function(tabId) {
        var iframe = document.getElementById('iframe-' + tabId);
        if (!iframe) return;

        try {
            var doc = iframe.contentDocument || iframe.contentWindow.document;
            var newUrl = iframe.contentWindow.location.pathname + iframe.contentWindow.location.search;
            var newTitle = doc.title.replace(' - Kanuni Tekstil', '');
            
            // Yanda açılan menünün iframe içinden tıklanınca kapanmasını sağla
            $(doc).on('click', function(e) {
                if ($('#dropdownSidebar').is(':visible')) {
                    $('#dropdownSidebar').slideUp(300);
                }
            });
            
            this.openTabs[tabId].url = newUrl;
            
            // Update tab title if changed
            if (newTitle && newTitle !== 'undefined') {
                this.openTabs[tabId].title = newTitle;
                $('#btn-' + tabId + ' .tab-title').text(newTitle).attr('title', newTitle);
            }

            // If this is the active tab, update top URL
            if ($('#btn-' + tabId).hasClass('active')) {
                history.replaceState({ tabId: tabId }, newTitle, newUrl);
            }
        } catch (e) {
            // Cross-origin or other error, ignore safely
            console.log("Iframe load cross-origin/error: ", e);
        }
    }
};

$(document).ready(function() {
    appTabs.init();
});


// Her yeni sayfaya geçişte iframe içinde yeni sekme açma (global intercept)
$(document).ready(function() {
    // 1. Sidebar linklerini yakala (Ana Sayfa'da iken)
    if (window.self === window.top) {
        $('#sidebarNavMenu').on('click', 'a', function(e) {
            var href = $(this).attr('href');
            var target = $(this).attr('target');
            
            // Allow default for empty, javascript:, hash, blank, or submenu toggles
            if (!href || href === '#' || href.startsWith('javascript:') || target === '_blank' || $(this).hasClass('no-tab-link')) return;
            
            // Eğer Ana Sayfa'ya tıklandıysa sayfayı gerçekten yönlendirebiliriz veya main-tab'e dönebiliriz
            if (href === '/' || href.toLowerCase() === '/home/index') {
                e.preventDefault();
                if (window.location.pathname !== '/' && window.location.pathname.toLowerCase() !== '/home/index' && window.location.pathname.toLowerCase() !== '/home') {
                    window.location.href = '/';
                } else {
                    appTabs.switchTab('main-tab');
                }
                return;
            }
            
            // Engelle
            e.preventDefault();
            
            var title = $(this).attr('data-tab-title') || $(this).find('.nav-text').text().trim() || 'Yeni Sekme';
            var iconClass = $(this).find('.menu-icon').attr('class');
            if (iconClass) {
                iconClass = iconClass.replace('menu-icon', '').trim();
            } else {
                iconClass = 'fa-solid fa-file';
            }
            
            appTabs.openNewTab(title, href, iconClass);
            
            // Mobilde menüyü kapatmak isterseniz
            // $('#dropdownSidebar').slideUp(300);
        });
    }

    // 2. Iframe içindeki linkleri yakala
    if (window.self !== window.top && window.parent.appTabs) {
        $(document).on('click', 'a', function(e) {
            if (e.isDefaultPrevented()) return;
            var href = $(this).attr('href');
            var target = $(this).attr('target');
            if (href && !href.startsWith('javascript:') && !href.startsWith('#') && target !== '_blank' && !$(this).attr('download') && !$(this).hasClass('no-tab-link')) {
                if (href.toLowerCase().indexOf('export') > -1 || href.toLowerCase().indexOf('download') > -1) return;
                
                e.preventDefault();
                var title = $(this).attr('data-tab-title');
                if (!title) {
                    title = $(this).attr('title') || $(this).text().trim() || 'Yeni Sayfa';
                    title = title.replace(/<[^>]*>?/gm, '').trim();
                    if (title.length > 25) title = title.substring(0, 25) + '...';
                }
                
                var icon = $(this).attr('data-tab-icon');
                if (!icon) {
                    var $icon = $(this).find('i');
                    if ($icon.length > 0) {
                        icon = $icon.attr('class').replace('tab-icon', '').replace('menu-icon', '').trim();
                    } else {
                        icon = 'fa-solid fa-file';
                    }
                }
                window.parent.appTabs.openNewTab(title, href, icon);
            }
        });
    }
});
