/**
 * @module api
 * Provides functions to interact with the backend API.
 */

/**
 * @typedef {Object} BaseConfig
 * @property {string} [salt]
 * @property {string} [type]
 * @property {string} [path]
 *
 * @typedef {Object} ServerConfig
 * @property {string[]} [tags]
 * @property {string} [type]
 * @property {string} [format]
 * @property {string} [path]
 *
 * @typedef {Object} UserConfig
 * @property {Object<string, ServerConfig>} [servers]
 * @property {string[]} [outbounds]
 * @property {Object} [custom_rules]
 *
 * @typedef {Object} AppConfig
 * @property {BaseConfig} [base_config]
 * @property {Object<string, ServerConfig>} [servers]
 * @property {Object<string, UserConfig>} [users]
 */

/**
 * Gets HTTP headers including the admin token.
 * @returns {Record<string, string>}
 */
const getHeaders = () => {
    const token = localStorage.getItem('adminToken');
    return {
        'Content-Type': 'application/json',
        'Authorization': token ? `Bearer ${token}` : ''
    };
};

/**
 * Verifies admin token with the server.
 * @param {string} token 
 * @returns {Promise<boolean>}
 */
export async function verifyToken(token) {
    localStorage.setItem('adminToken', token);
    const res = await fetch('/api/auth/verify', {
        method: 'POST',
        headers: getHeaders()
    });
    if (!res.ok) {
        localStorage.removeItem('adminToken');
        return false;
    }
    return true;
}

/**
 * Fetches the configuration from the server.
 * @returns {Promise<AppConfig>}
 */
export async function fetchConfig() {
    const res = await fetch('/api/config', { headers: getHeaders() });
    if (!res.ok) throw new Error('Failed to fetch config');
    return res.json();
}

/**
 * Saves the configuration to the server.
 * @param {AppConfig} configObj 
 * @returns {Promise<boolean>}
 */
export async function saveConfig(configObj) {
    const res = await fetch('/api/config', {
        method: 'POST',
        headers: getHeaders(),
        body: JSON.stringify(configObj, null, 4)
    });
    if (!res.ok) {
        const errorText = await res.text();
        throw new Error(errorText || 'Failed to save config');
    }
    return true;
}
