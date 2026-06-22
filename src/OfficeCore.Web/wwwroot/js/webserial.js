let port;
let reader;
let dotNetRef;
let targetEnrollEmployeeId = null;

export async function initSerial(dotNetReference) {
    dotNetRef = dotNetReference;

    // Check if we already have an authorized port
    if ("serial" in navigator) {
        try {
            const ports = await navigator.serial.getPorts();
            if (ports.length > 0) {
                const savedPort = ports[0];

                // If we already have this port open (e.g. navigated away and back), just re-attach
                if (port && port === savedPort && port.readable) {
                    dotNetRef.invokeMethodAsync('OnSerialConnected');
                    return;
                }

                // Close stale open port from a different instance if needed
                if (savedPort.readable) {
                    // Port is already open by another context — attach to it
                    port = savedPort;
                    dotNetRef.invokeMethodAsync('OnSerialConnected');
                    readLoop();
                    return;
                }

                // Port exists but is closed — open it
                port = savedPort;
                await port.open({ baudRate: 9600 });
                dotNetRef.invokeMethodAsync('OnSerialConnected');
                readLoop();
            }
        } catch (e) {
            console.warn("Auto-connect skipped:", e.message);
        }
    }
    console.log("Web Serial API initialized.");
}

export async function connectSerial() {
    if (!('serial' in navigator)) {
        throw new Error("Web Serial API not supported in this browser.");
    }
    
    try {
        port = await navigator.serial.requestPort();
        await port.open({ baudRate: 9600 });
        
        dotNetRef.invokeMethodAsync('OnSerialConnected');
        
        // Start listening
        readLoop();
    } catch (e) {
        throw new Error("Failed to connect to USB: " + e.message);
    }
}

export async function disconnectSerial() {
    if (reader) {
        await reader.cancel();
    }
    if (port) {
        await port.close();
    }
    port = null;
    dotNetRef.invokeMethodAsync('OnSerialDisconnected');
}

export async function startEnrollment(employeeId) {
    if (!port || !port.writable) {
        throw new Error("Serial port not writable.");
    }
    
    targetEnrollEmployeeId = employeeId;
    
    const writer = port.writable.getWriter();
    const encoder = new TextEncoder();
    await writer.write(encoder.encode("ENROLL\n"));
    writer.releaseLock();
}

export async function cancelEnrollment() {
    if (!port || !port.writable) {
        return;
    }
    
    targetEnrollEmployeeId = null;
    const writer = port.writable.getWriter();
    const encoder = new TextEncoder();
    await writer.write(encoder.encode("CANCEL\n"));
    writer.releaseLock();
}

export async function sendWifiConfig(ssid, password, serverIp) {
    if (!port || !port.writable) {
        throw new Error("Serial port not writable.");
    }
    
    const writer = port.writable.getWriter();
    const encoder = new TextEncoder();
    const command = `SET_WIFI:${ssid},${password},${serverIp}\n`;
    await writer.write(encoder.encode(command));
    writer.releaseLock();
}

async function readLoop() {
    while (port && port.readable) {
        const textDecoder = new TextDecoderStream();
        const readableStreamClosed = port.readable.pipeTo(textDecoder.writable);
        reader = textDecoder.readable.getReader();
        
        try {
            let buffer = "";
            while (true) {
                const { value, done } = await reader.read();
                if (done) break;
                
                buffer += value;
                let lines = buffer.split("\n");
                
                if (lines.length > 1) {
                    for (let i = 0; i < lines.length - 1; i++) {
                        processLine(lines[i].trim());
                    }
                    buffer = lines[lines.length - 1];
                }
            }
        } catch (error) {
            console.error("Read error: ", error);
        } finally {
            reader.releaseLock();
        }
    }
}

function processLine(line) {
    if (!line) return;
    
    console.log("USB IN: ", line);
    
    if (line.startsWith("ATTENDANCE:")) {
        let fingerprintId = parseInt(line.split(":")[1]);
        if (!isNaN(fingerprintId)) {
            dotNetRef.invokeMethodAsync('OnSerialScanEvent', fingerprintId);
        }
    }
    else if (line.startsWith("ENROLL_SUCCESS:")) {
        let fingerprintId = parseInt(line.split(":")[1]);
        if (!isNaN(fingerprintId) && targetEnrollEmployeeId) {
            dotNetRef.invokeMethodAsync('OnSerialEnrollSuccess', targetEnrollEmployeeId, fingerprintId);
            targetEnrollEmployeeId = null;
        }
    }
    else if (line.startsWith("INFO:") || line.startsWith("ERROR:") || line.startsWith("MODE:")) {
        if (targetEnrollEmployeeId) {
            // e.g. "INFO: Waiting for valid finger" -> "Waiting for valid finger"
            let msg = line.substring(line.indexOf(":") + 1).trim();
            dotNetRef.invokeMethodAsync('OnSerialEnrollStatus', msg);
            
            if (line.startsWith("ERROR:")) {
                targetEnrollEmployeeId = null; // stop tracking if error
            }
        }
    }
}
