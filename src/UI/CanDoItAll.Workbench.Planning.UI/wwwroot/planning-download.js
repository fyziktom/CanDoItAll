export async function download(fileName, mediaType, stream) {
    const bytes = await stream.arrayBuffer();
    const url = URL.createObjectURL(new Blob([bytes], { type: mediaType }));
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    try {
        anchor.click();
    } finally {
        anchor.remove();
        URL.revokeObjectURL(url);
    }
}
