window.candoitallTuning = {
    readClipboardImage: async function () {
        if (!navigator.clipboard || !navigator.clipboard.read) {
            return null;
        }

        const items = await navigator.clipboard.read();
        for (const item of items) {
            const imageType = item.types.find(type => type.startsWith("image/"));
            if (!imageType) {
                continue;
            }

            const blob = await item.getType(imageType);
            const buffer = await blob.arrayBuffer();
            let binary = "";
            const bytes = new Uint8Array(buffer);
            for (let index = 0; index < bytes.length; index++) {
                binary += String.fromCharCode(bytes[index]);
            }

            return {
                fileName: `clipboard-${Date.now()}.${imageType.split("/")[1] || "png"}`,
                contentType: imageType,
                contentBase64: btoa(binary)
            };
        }

        return null;
    }
};
