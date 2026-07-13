/**
 * @module ui
 * Handles DOM generation for config items.
 */

/**
 * @typedef {import('./api.js').ServerConfig} ServerConfig
 * @typedef {import('./api.js').UserConfig} UserConfig
 */

export function renameDictKey(dictObj, oldKey, newKey) {
    if (!newKey || newKey === oldKey) return false;
    const newDict = {};
    const keys = Object.keys(dictObj);
    for (const k of keys) {
        if (k === oldKey) newDict[newKey] = dictObj[oldKey];
        else newDict[k] = dictObj[k];
    }
    for (const k of keys) delete dictObj[k];
    for (const [k, v] of Object.entries(newDict)) dictObj[k] = v;
    return true;
}

/**
 * Creates a chip element with a remove button and drag-and-drop support.
 * @param {string} text 
 * @param {function(): void} onRemove 
 * @param {Object} [dndContext] Optional { list, index, renderCallback, onUpdate }
 * @returns {HTMLDivElement}
 */
export function createChip(text, onRemove, dndContext = null) {
    const div = document.createElement('div');
    div.className = 'chip';
    div.innerHTML = `
        <span></span>
        <span class="remove">×</span>
    `;
    div.querySelector('span').textContent = text;
    div.querySelector('.remove').addEventListener('click', onRemove);

    const span = div.querySelector('span');
    span.style.cursor = 'text';
    span.title = "Double click to rename";
    span.addEventListener('dblclick', (e) => {
        e.stopPropagation();
        if (!dndContext) return;
        const newText = prompt("Rename to:", text);
        if (newText && newText !== text) {
            dndContext.list[dndContext.index] = newText;
            dndContext.renderCallback();
            dndContext.onUpdate();
        }
    });

    if (dndContext) {
        div.setAttribute('draggable', true);
        div.addEventListener('dragstart', (e) => {
            e.dataTransfer.effectAllowed = 'move';
            div.classList.add('dragging');
            window.__draggedChip = dndContext;
        });
        div.addEventListener('dragend', () => {
            div.classList.remove('dragging');
            window.__draggedChip = null;
        });
        div.addEventListener('dragover', (e) => {
            e.preventDefault();
        });
        div.addEventListener('drop', (e) => {
            e.preventDefault();
            const dragged = window.__draggedChip;
            if (dragged && dragged.list === dndContext.list && dragged.index !== dndContext.index) {
                const item = dragged.list.splice(dragged.index, 1)[0];
                dndContext.list.splice(dndContext.index, 0, item);
                dndContext.renderCallback();
                dndContext.onUpdate();
            }
        });
    }

    return div;
}

/**
 * Creates an "Add" chip element.
 * @param {string} text 
 * @param {function(): void} onClick 
 * @returns {HTMLDivElement}
 */
export function createAddChip(text, onClick) {
    const div = document.createElement('div');
    div.className = 'chip add';
    div.textContent = text;
    div.addEventListener('click', onClick);
    return div;
}

export function makeCardDraggable(cardDiv, keyName, dictObj, onReorder) {
    const header = cardDiv.querySelector('.card-header');
    header.style.cursor = 'grab';
    cardDiv.setAttribute('draggable', true);

    cardDiv.addEventListener('dragstart', (e) => {
        cardDiv.classList.add('dragging');
        window.__draggedCard = { key: keyName, dict: dictObj };
        e.stopPropagation();
    });
    cardDiv.addEventListener('dragend', () => {
        cardDiv.classList.remove('dragging');
        window.__draggedCard = null;
    });
    cardDiv.addEventListener('dragover', (e) => {
        e.preventDefault();
    });
    cardDiv.addEventListener('drop', (e) => {
        e.preventDefault();
        e.stopPropagation();
        const dragged = window.__draggedCard;
        if (dragged && dragged.dict === dictObj && dragged.key !== keyName) {
            const newDict = {};
            const keys = Object.keys(dictObj);
            for (const k of keys) {
                if (k === dragged.key) continue;
                if (k === keyName) {
                    newDict[dragged.key] = dictObj[dragged.key];
                }
                newDict[k] = dictObj[k];
            }
            for (const k of keys) delete dictObj[k];
            for (const [k, v] of Object.entries(newDict)) dictObj[k] = v;
            onReorder();
        }
    });
}

/**
 * Renders a server card.
 * @param {string} name 
 * @param {ServerConfig} server 
 * @param {function(): void} onUpdate 
 * @param {Object} dictObj The dictionary containing this card
 * @returns {HTMLDivElement}
 */
export function renderServerCard(name, server, onUpdate, dictObj) {
    const div = document.createElement('div');
    div.className = 'item-card';
    
    div.innerHTML = `
        <div class="card-header">
            <h3 title="Double click to rename" style="cursor:text;"></h3>
        </div>
        <div class="form-group">
            <label>Type</label>
            <input type="text" class="server-type">
        </div>
        <div class="form-group">
            <label>Format</label>
            <input type="text" class="server-format">
        </div>
        <div class="form-group">
            <label>Path</label>
            <input type="text" class="server-path">
        </div>
        <div class="form-group">
            <label>Tags</label>
            <div class="chips-container tags-container"></div>
        </div>
    `;

    // Safely assign text and values to prevent XSS
    div.querySelector('h3').textContent = name;
    div.querySelector('.server-type').value = server.type || '';
    div.querySelector('.server-format').value = server.format || '';
    div.querySelector('.server-path').value = server.path || '';

    // Handle inputs
    const inputs = div.querySelectorAll('input');
    inputs.forEach(input => {
        input.addEventListener('change', () => {
            server.type = div.querySelector('.server-type').value;
            if (!server.type) delete server.type;
            
            server.format = div.querySelector('.server-format').value;
            if (!server.format) delete server.format;
            
            server.path = div.querySelector('.server-path').value;
            if (!server.path) delete server.path;
            
            onUpdate();
        });
    });

    // Render tags
    const tagsContainer = div.querySelector('.tags-container');
    const renderTags = () => {
        tagsContainer.innerHTML = '';
        (server.tags || []).forEach((tag, idx) => {
            tagsContainer.appendChild(createChip(tag, () => {
                server.tags.splice(idx, 1);
                if (server.tags.length === 0) delete server.tags;
                renderTags();
                onUpdate();
            }, { list: server.tags, index: idx, renderCallback: renderTags, onUpdate }));
        });
        tagsContainer.appendChild(createAddChip('+ Add Tag', () => {
            const newTag = prompt("Enter new tag:");
            if (newTag) {
                if (!server.tags) server.tags = [];
                server.tags.push(newTag);
                renderTags();
                onUpdate();
            }
        }));
    };
    renderTags();

    const h3 = div.querySelector('h3');
    h3.addEventListener('dblclick', (e) => {
        e.stopPropagation();
        if (!dictObj) return;
        const newName = prompt(`Rename '${name}' to:`, name);
        if (renameDictKey(dictObj, name, newName)) onUpdate();
    });

    if (dictObj) makeCardDraggable(div, name, dictObj, onUpdate);
    return div;
}

/**
 * Renders a user card.
 * @param {string} name 
 * @param {UserConfig} user 
 * @param {function(): void} onUpdate 
 * @param {Object} dictObj The dictionary containing this card
 * @param {string} salt The base_config salt
 * @returns {HTMLDivElement}
 */
export function renderUserCard(name, user, onUpdate, dictObj, salt) {
    const div = document.createElement('div');
    div.className = 'item-card';
    
    div.innerHTML = `
        <div class="card-header" style="display:flex; justify-content:space-between; align-items:center;">
            <h3 style="margin:0; cursor:text;" title="Double click to rename"></h3>
            <div style="display:flex; gap: 8px;">
                <button class="btn-copy-link add-btn" style="padding:4px 8px; font-size:0.8rem; background:rgba(255,255,255,0.1);">🔗 Copy Links</button>
            </div>
        </div>
        <div class="form-group">
            <label>Outbounds (Servers & Groups)</label>
            <div class="chips-container outbounds-container"></div>
        </div>
        <div class="form-group">
            <details>
                <summary style="cursor: pointer; font-weight: bold; margin-bottom: 8px; color: var(--primary-color);">Personal Servers</summary>
                <div class="card-grid user-servers-container" style="background: rgba(0,0,0,0.1); padding: 10px; border-radius: 8px;"></div>
                <button class="btn-add-user-server add-btn" style="margin-top: 10px; font-size: 0.8rem; padding: 4px 8px;">+ Add Personal Server</button>
            </details>
        </div>
        <div class="form-group">
            <label>Custom Rules (JSON)</label>
            <textarea class="custom-rules-editor" rows="5" placeholder="Enter valid JSON..."></textarea>
            <div class="error-msg hidden" style="color:var(--danger-color);font-size:0.8rem;margin-top:4px;">Invalid JSON</div>
        </div>
    `;

    // Safely assign properties
    div.querySelector('h3').textContent = name;
    if (user.custom_rules) {
        div.querySelector('.custom-rules-editor').value = JSON.stringify(user.custom_rules, null, 2);
    }

    // Render outbounds
    const outboundsContainer = div.querySelector('.outbounds-container');
    const renderOutbounds = () => {
        outboundsContainer.innerHTML = '';
        (user.outbounds || []).forEach((ob, idx) => {
            outboundsContainer.appendChild(createChip(ob, () => {
                user.outbounds.splice(idx, 1);
                if (user.outbounds.length === 0) delete user.outbounds;
                renderOutbounds();
                onUpdate();
            }, { list: user.outbounds, index: idx, renderCallback: renderOutbounds, onUpdate }));
        });
        outboundsContainer.appendChild(createAddChip('+ Add Outbound/Group', () => {
            const newOb = prompt("Enter server or group name:");
            if (newOb) {
                if (!user.outbounds) user.outbounds = [];
                user.outbounds.push(newOb);
                renderOutbounds();
                onUpdate();
            }
        }));
    };
    renderOutbounds();

    // Render personal servers
    const userServersContainer = div.querySelector('.user-servers-container');
    const renderUserServers = () => {
        userServersContainer.innerHTML = '';
        if (user.servers) {
            for (const [sName, srv] of Object.entries(user.servers)) {
                const srvCard = renderServerCard(sName, srv, () => {
                    renderUserServers();
                    onUpdate();
                }, user.servers);
                // Adjust styling for nested card
                srvCard.style.boxShadow = 'none';
                srvCard.style.border = '1px solid rgba(255,255,255,0.1)';
                srvCard.style.margin = '0';
                userServersContainer.appendChild(srvCard);
            }
        }
    };
    renderUserServers();

    div.querySelector('.btn-add-user-server').addEventListener('click', () => {
        const sName = prompt("Enter personal server name:");
        if (sName) {
            if (!user.servers) user.servers = {};
            user.servers[sName] = { type: "remote", format: "singbox", path: "", tags: [] };
            renderUserServers();
            onUpdate();
        }
    });

    // Custom Rules JSON editor
    const rulesEditor = div.querySelector('.custom-rules-editor');
    const rulesError = div.querySelector('.error-msg');
    rulesEditor.addEventListener('blur', () => {
        const val = rulesEditor.value.trim();
        if (!val) {
            delete user.custom_rules;
            rulesError.classList.add('hidden');
            onUpdate();
            return;
        }
        try {
            user.custom_rules = JSON.parse(val);
            rulesError.classList.add('hidden');
            rulesEditor.value = JSON.stringify(user.custom_rules, null, 2);
            onUpdate();
        } catch (e) {
            rulesError.classList.remove('hidden');
        }
    });

    const h3 = div.querySelector('h3');
    h3.addEventListener('dblclick', (e) => {
        e.stopPropagation();
        if (!dictObj) return;
        const newName = prompt(`Rename user '${name}' to:`, name);
        if (renameDictKey(dictObj, name, newName)) onUpdate();
    });

    // Copy Link logic
    const btnCopyLink = div.querySelector('.btn-copy-link');
    btnCopyLink.addEventListener('click', async () => {
        if (!salt) return;
        try {
            const encoder = new TextEncoder();
            const data = encoder.encode(`${name}.${salt}`);
            const sha256Buffer = await crypto.subtle.digest('SHA-256', data);
            const newHash = Array.from(new Uint8Array(sha256Buffer)).slice(0, 8).map(b => b.toString(16).padStart(2, '0')).join('');
            const linkNew = `${window.location.origin}/configs/${newHash}/${name}.json`;
            
            await navigator.clipboard.writeText(linkNew).catch(() => {});
            const oldText = btnCopyLink.innerHTML;
            btnCopyLink.innerHTML = "✅ Copied";
            setTimeout(() => btnCopyLink.innerHTML = oldText, 2000);
        } catch (e) {
            console.error(e);
        }
    });

    if (dictObj) makeCardDraggable(div, name, dictObj, onUpdate);
    return div;
}

/**
 * Renders an outbound group card.
 * @param {string} name 
 * @param {Object} groupsDict 
 * @param {function(): void} onUpdate 
 * @returns {HTMLDivElement}
 */
export function renderGroupCard(name, groupsDict, onUpdate) {
    const div = document.createElement('div');
    div.className = 'item-card';
    const groupList = groupsDict[name] || [];
    
    div.innerHTML = `
        <div class="card-header">
            <h3 title="Double click to rename" style="cursor:text;"></h3>
        </div>
        <div class="form-group">
            <label>Servers in Group</label>
            <div class="chips-container servers-container"></div>
        </div>
    `;

    div.querySelector('h3').textContent = name;

    const serversContainer = div.querySelector('.servers-container');
    const renderServers = () => {
        serversContainer.innerHTML = '';
        groupList.forEach((srv, idx) => {
            serversContainer.appendChild(createChip(srv, () => {
                groupList.splice(idx, 1);
                if (groupList.length === 0) delete groupsDict[name];
                renderServers();
                onUpdate();
            }, { list: groupList, index: idx, renderCallback: renderServers, onUpdate }));
        });
        serversContainer.appendChild(createAddChip('+ Add Server', () => {
            const newSrv = prompt("Enter server name:");
            if (newSrv) {
                groupList.push(newSrv);
                renderServers();
                onUpdate();
            }
        }));
    };
    renderServers();

    const h3 = div.querySelector('h3');
    h3.addEventListener('dblclick', (e) => {
        e.stopPropagation();
        const newName = prompt(`Rename group '${name}' to:`, name);
        if (renameDictKey(groupsDict, name, newName)) onUpdate();
    });

    makeCardDraggable(div, name, groupsDict, onUpdate);
    return div;
}
