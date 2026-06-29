let matrix = null;
let forcedMap = {};

// ======================
// INIT
// ======================
loadMatrix();


// ======================
// LOAD MATRIX (inputs + outputs + forced state)
// ======================
async function loadMatrix(selectedOutput = null) {
    showBusy("Loading Cross Points");

    const response = await fetch('/api/matrix');
    matrix = await response.json();

    // Build forced lookup
    forcedMap = {};
    (matrix.forcedCrosspoints || []).forEach(cp => {
        forcedMap[`${cp.input}-${cp.output}`] = cp.state;
    });

    await populateOutputs(selectedOutput);

    hideBusy();
}


// ======================
// UI HELPERS
// ======================
function showBusy(message = "Loading...") {
    const overlay = document.getElementById("busyOverlay");
    overlay.innerText = message;
    overlay.style.display = "flex";
}

function hideBusy() {
    document.getElementById("busyOverlay").style.display = "none";
}


// ======================
// OUTPUT DROPDOWN
// ======================
async function populateOutputs(selectedOutput = null) {
    const select = document.getElementById('outputSelect');

    select.innerHTML = '';

    matrix.outputs.forEach(output => {
        const option = document.createElement('option');
        option.value = output.number;
        option.textContent = `${output.number} - ${output.name}`;
        select.appendChild(option);
    });

    // Manual output change
    select.onchange = async () => {
        showBusy("Loading Cross Points");
        await loadOutput(select.value);
        hideBusy();
    };

    if (
        selectedOutput &&
        matrix.outputs.some(o => String(o.number) === String(selectedOutput))
    ) {
        select.value = selectedOutput;
        await loadOutput(selectedOutput);
    }
    else if (matrix.outputs.length > 0) {
        select.value = matrix.outputs[0].number;
        await loadOutput(matrix.outputs[0].number);
    }
}


// ======================
// LOAD CROSSPOINTS FOR OUTPUT
// ======================
async function loadOutput(output) {
    const response = await fetch(`/api/output/${output}`);
    const crosspoints = await response.json();

    renderCrosspoints(output, crosspoints);
}


// ======================
// LOOKUP FOR FORCE STATE
// ======================
function getForceState(input, output) {
    return forcedMap[`${input}-${output}`] || "Default";
}


// ======================
// RENDER TABLE
// ======================
function renderCrosspoints(output, crosspoints) {
    const body = document.getElementById('crosspoints');
    body.innerHTML = '';

    crosspoints.forEach(cp => {

        const row = document.createElement('tr');

        // ======================
        // INPUT NAME
        // ======================
        const inputCell = document.createElement('td');
        inputCell.textContent = `${cp.number} - ${cp.name}`;

        // ======================
        // STATUS CELL
        // ======================
        const statusCell = document.createElement('td');

        const forceState = getForceState(cp.number, parseInt(output));

        let led = "";
        let title = "";

        if (forceState === "ForceOn") {
            led = "🟢";
            title = "Forced On";
        }
        else if (forceState === "ForceOff") {
            led = "🔴";
            title = "Forced Off";
        }
        else {
            if (cp.connected) {
                led = "🟢";
                title = "As Defined (On)";
            } else {
                led = "⚫";
                title = "As Defined (Off)";
            }
        }

        statusCell.innerHTML = `<span title="${title}" style="font-size:16px;">${led}</span>`;

        // ======================
        // OVERRIDE DROPDOWN
        // ======================
        const overrideCell = document.createElement('td');

        const select = document.createElement('select');

        [
            { value: "Default", label: "As Defined" },
            { value: "ForceOn", label: "Forced On" },
            { value: "ForceOff", label: "Forced Off" }
        ].forEach(state => {
            const option = document.createElement("option");

            option.value = state.value;
            option.textContent = state.label;

            if (forceState === state.value) {
                option.selected = true;
            }

            select.appendChild(option);
        });

        select.addEventListener("change", async () => {
            showBusy("Saving Override");

            try {

                if (select.value === "Default") {
                    await fetch("/api/crosspoint/clear", {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({
                            input: cp.number,
                            output: parseInt(output)
                        })
                    });
                } else {
                    await fetch("/api/crosspoint/force", {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({
                            input: cp.number,
                            output: parseInt(output),
                            state: select.value
                        })
                    });
                }

                // Reload everything while keeping the current output selected.
                // The loading overlay remains visible until the table is rebuilt.
                await loadMatrix(output);

            } catch (err) {
                console.error(err);
                alert("Failed to save override.");
                hideBusy();
            }
        });

        overrideCell.appendChild(select);

        // ======================
        // BUILD ROW
        // ======================
        row.appendChild(inputCell);
        row.appendChild(statusCell);
        row.appendChild(overrideCell);

        body.appendChild(row);
    });
}