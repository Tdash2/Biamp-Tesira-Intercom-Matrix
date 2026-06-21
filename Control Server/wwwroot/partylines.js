let partyLines = [];
let selected = null;

async function loadPartyLines() {
    const res = await fetch('/api/partylines');
    partyLines = await res.json();

    renderList();
}

function renderList() {
    const list = document.getElementById('plList');
    list.innerHTML = '';

    partyLines.forEach(pl => {
        const li = document.createElement('li');

        li.textContent = `${pl.id} - ${pl.name}`;

        li.onclick = () => selectPartyLine(pl.id);

        list.appendChild(li);
    });
}

function selectPartyLine(id) {
    selected = partyLines.find(x => x.id === id);

    renderDetails();
}

function renderDetails() {
    const div = document.getElementById('details');

    if (!selected)
        return;

    div.innerHTML = `
        <h3>${selected.name}</h3>

       <h4>Inputs</h4>

${selected.inputs.map(i => `
    <div>
        Input ${i}
        <button onclick="removeInput(${i})">X</button>
    </div>
`).join('')}
 

        <select id="addInputSelect"></select>
        <button onclick="addInput()">Add Input</button>

        <h4>Outputs</h4>

${selected.outputs.map(o => `
    <div>
        Output ${o}
        <button onclick="removeOutput(${o})">X</button>
    </div>
`).join('')}


        <select id="addOutputSelect"></select>
        <button onclick="addOutput()">Add Output</button>
    `;

    populateDropdowns();
}

async function removeInput(input) {
    await fetch('/api/partyline/remove-input',
        {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                plId: selected.id,
                input
            })
        });

    await loadPartyLines();
    selectPartyLine(selected.id);
}
async function removeOutput(output) {
    await fetch('/api/partyline/remove-output',
        {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                plId: selected.id,
                output
            })
        });

    await loadPartyLines();
    selectPartyLine(selected.id);
}

async function populateDropdowns() {
    const res = await fetch('/api/matrix');
    const data = await res.json();

    const inSel = document.getElementById('addInputSelect');
    const outSel = document.getElementById('addOutputSelect');

    inSel.innerHTML = '';
    outSel.innerHTML = '';

    data.inputs.forEach(i => {
        const opt = document.createElement('option');
        opt.value = i.number;
        opt.textContent = `${i.number} - ${i.name}`;
        inSel.appendChild(opt);
    });

    data.outputs.forEach(o => {
        const opt = document.createElement('option');
        opt.value = o.number;
        opt.textContent = `${o.number} - ${o.name}`;
        outSel.appendChild(opt);
    });
}

async function createPartyLine() {
    const name = document.getElementById('newPlName').value;

    await fetch('/api/partyline', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name })
    });

    document.getElementById('newPlName').value = '';

    await loadPartyLines();
}

async function addInput() {
    const input = document.getElementById('addInputSelect').value;

    await fetch('/api/partyline/add-input', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            plId: selected.id,
            input: parseInt(input)
        })
    });

    await loadPartyLines();
    selectPartyLine(selected.id);
}

async function addOutput() {
    const output = document.getElementById('addOutputSelect').value;

    await fetch('/api/partyline/add-output', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            plId: selected.id,
            output: parseInt(output)
        })
    });

    await loadPartyLines();
    selectPartyLine(selected.id);
}

loadPartyLines();