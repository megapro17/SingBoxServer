/**
 * @module app
 * Main application logic
 */

/**
 * @typedef {import('./api.js').AppConfig} AppConfig
 */

import { verifyToken, fetchConfig, saveConfig } from './api.js';
import { renderServerCard, renderUserCard, renderGroupCard } from './ui.js';

/** @type {AppConfig | null} */
let configData = null;

const dom = {
    authScreen: document.getElementById('auth-screen'),
    appScreen: document.getElementById('app-screen'),
    btnLogin: document.getElementById('btn-login'),
    tokenInput: document.getElementById('admin-token'),
    authError: document.getElementById('auth-error'),
    toast: document.getElementById('toast'),
    btnSave: document.getElementById('btn-save'),
    btnLogout: document.getElementById('btn-logout'),
    serversContainer: document.getElementById('servers-container'),
    usersContainer: document.getElementById('users-container'),
    groupsContainer: document.getElementById('groups-container'),
    btnAddServer: document.getElementById('btn-add-server'),
    btnAddUser: document.getElementById('btn-add-user'),
    btnAddGroup: document.getElementById('btn-add-group'),
    tabs: document.querySelectorAll('.tab'),
    baseSalt: document.getElementById('base-salt'),
    baseType: document.getElementById('base-type'),
    basePath: document.getElementById('base-path'),
};

/**
 * Shows a toast notification.
 * @param {string} message 
 * @param {boolean} [isError=false] 
 */
function showToast(message, isError = false) {
    dom.toast.textContent = message;
    dom.toast.className = `toast ${isError ? 'error' : 'success'}`;
    setTimeout(() => {
        dom.toast.classList.add('hidden');
    }, 3000);
}

// Auth
dom.btnLogin.addEventListener('click', async () => {
    const token = dom.tokenInput.value;
    if (!token) return;
    const isValid = await verifyToken(token);
    if (isValid) {
        dom.authScreen.classList.add('hidden');
        dom.appScreen.classList.remove('hidden');
        await loadData();
    } else {
        dom.authError.textContent = "Invalid Token";
        dom.authError.classList.remove('hidden');
    }
});

dom.btnLogout.addEventListener('click', () => {
    localStorage.removeItem('adminToken');
    window.location.reload();
});

// Auto-login
window.addEventListener('DOMContentLoaded', async () => {
    const existingToken = localStorage.getItem('adminToken');
    if (existingToken) {
        const isValid = await verifyToken(existingToken);
        if (isValid) {
            dom.authScreen.classList.add('hidden');
            dom.appScreen.classList.remove('hidden');
            await loadData();
        }
    }
});

// Tabs
dom.tabs.forEach(tab => {
    tab.addEventListener('click', () => {
        document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
        document.querySelectorAll('.tab-content').forEach(c => c.classList.add('hidden'));
        tab.classList.add('active');
        document.getElementById(tab.dataset.target).classList.remove('hidden');
    });
});

async function loadData() {
    try {
        configData = await fetchConfig();
        render();
    } catch (e) {
        showToast(e.message, true);
    }
}

function render() {
    if (!configData) return;

    // Base config
    dom.baseSalt.value = configData.base_config?.salt || '';
    dom.baseType.value = configData.base_config?.type || '';
    dom.basePath.value = configData.base_config?.path || '';

    // Servers
    dom.serversContainer.innerHTML = '';
    if (configData.servers) {
        for (const [name, server] of Object.entries(configData.servers)) {
            dom.serversContainer.appendChild(renderServerCard(name, server, render, configData.servers));
        }
    }

    // Users
    dom.usersContainer.innerHTML = '';
    if (configData.users) {
        const salt = configData.base_config?.salt || "";
        for (const [name, user] of Object.entries(configData.users)) {
            dom.usersContainer.appendChild(renderUserCard(name, user, render, configData.users, salt));
        }
    }

    // Groups
    dom.groupsContainer.innerHTML = '';
    if (configData.outbound_groups) {
        for (const [name, groupList] of Object.entries(configData.outbound_groups)) {
            dom.groupsContainer.appendChild(renderGroupCard(name, configData.outbound_groups, render));
        }
    }

    // Links Tab
    const linksTextarea = document.getElementById('links-textarea');
    if (linksTextarea && configData.users && configData.base_config?.salt) {
        linksTextarea.value = 'Generating links...';
        const salt = configData.base_config.salt;
        const baseUrl = window.location.origin;
        
        const generateAllLinks = async () => {
            let output = '';
            for (const name of Object.keys(configData.users)) {
                const encoder = new TextEncoder();
                const data = encoder.encode(`${name}.${salt}`);

                const sha1Buffer = await crypto.subtle.digest('SHA-1', data);
                const legacyHash = Array.from(new Uint8Array(sha1Buffer)).map(b => b.toString(16).padStart(2, '0')).join('');

                const sha256Buffer = await crypto.subtle.digest('SHA-256', data);
                const newHash = Array.from(new Uint8Array(sha256Buffer)).slice(0, 8).map(b => b.toString(16).padStart(2, '0')).join('');

                output += `=== User: ${name} ===\n`;
                output += `${baseUrl}/configs/${newHash}/${name}.json\n`;
                output += `${baseUrl}/configs/${legacyHash}/${name}.json\n\n`;
            }
            linksTextarea.value = output.trim();
        };
        generateAllLinks();
    }
}

// Add actions
dom.btnAddServer?.addEventListener('click', () => {
    if (!configData) return;
    const name = prompt("Enter new server name:");
    if (name) {
        if (!configData.servers) configData.servers = {};
        configData.servers[name] = { type: "vless", format: "link", path: "", tags: [] };
        render();
    }
});

dom.btnAddUser?.addEventListener('click', () => {
    if (!configData) return;
    const name = prompt("Enter new user name:");
    if (name) {
        if (!configData.users) configData.users = {};
        configData.users[name] = { use_shared_servers: true, outbounds: [] };
        render();
    }
});

dom.btnAddGroup?.addEventListener('click', () => {
    if (!configData) return;
    const name = prompt("Enter new group name:");
    if (name) {
        if (!configData.outbound_groups) configData.outbound_groups = {};
        configData.outbound_groups[name] = [];
        render();
    }
});

// Save
dom.btnSave.addEventListener('click', async () => {
    if (!configData) return;

    // Update base config
    if (!configData.base_config) configData.base_config = {};
    configData.base_config.salt = dom.baseSalt.value;
    configData.base_config.type = dom.baseType.value;
    configData.base_config.path = dom.basePath.value;

    try {
        await saveConfig(configData);
        showToast('Configuration saved successfully!');
    } catch (e) {
        showToast(e.message, true);
    }
});
