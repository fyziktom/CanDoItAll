(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const shared = root.canvasWorkbenchModule;
    if (!shared) { throw new Error('CanvasLib workbench foundation must load before 04-context-menu-and-composer.js.'); }
    const workbenchInternals = new Proxy({}, {
        get(_target, property) {
            return shared.workbenchInternals?.[property];
        }
    });
    const { contextSubmenuHoverDelayMs, MIN_ZOOM, MAX_ZOOM, selectionModel, getRequiredRootService, getTextMeasureService, getViewportControllerService, getAnimationTimelineService, clear, createElement, createSvgElement, normalizeInputField, normalizeInputValue, clamp, debounce, now, createWorkbenchMetrics, formatMetricDuration, cloneWorkbenchMetrics, incrementMetric, resetLastDragPatchMetrics, recordDragPatchMetrics, round, normalizeAction, normalizeAnnotation, normalizeCompactPath, normalizeDiagnosticsOptions, normalizeMinimapOptions, normalizeClipboardOptions, normalizeTooltipPopoverOptions, normalizeMarqueeOptions, normalizeSnapGuideOptions, normalizeConnectorAnchorOptions, normalizeTransformHandleOptions, normalizeGroupFrame, normalizeProgressPercent, normalizeMenuActionScale, normalizeSurface, toSelectionSet, toCollapsedSet, getDefaultNodeSize, resolveBaseNodeSize, estimateNodeSizeFromText, getNodeSize, buildNodeLookup, isNodeVisible, getVisibleNodes, getProjectionOverscanPx, collectProjectedContextNodeIds, isNodeProjectedInViewport, getProjectedNodes, getBaseNodePosition, getNodeDepth, getNodeMobility, buildResolvedLayoutKey, buildLayoutItems, getCollisionPaddingX, getCollisionPaddingY, getOverlapDelta, chooseCollisionAxis, resolveCollisionDirection, applyCollisionSeparation, enforceParentClearance, enforceSiblingSpacing, relaxTowardBase, computeResolvedNodePositions, ensureLayoutPositions, getNodePosition, getSceneBounds, clampPanToScene, setPan, syncMenuScaleCss, serializeState, legacyApplySceneTransform, cancelViewportAnimation, updateViewportTransform, animateViewportTransition, getLinkAnchorPoint, getLinkRetainedKey, getLinkPathData, updateLinkElement, shouldRenderArrow, getExpandedFrameNodeIds, getFrameRetainedKey, createFrameElement, updateFrameElement, getFrameBounds, legacyRenderGroupFrames, resolveChipToneClass, createProgressMarker, resolveProgressDisplay, createProgressBadge, resolveProgressPresetBadgeOptions, resolveMarkerGlyph, createMarkerBadge, createPriorityBadge, appendNodeIndicators, renderInlineTextNode, createNodeMedia, createCompactPathButton, renderStandardNode, createRetainedNodeElement, getNodeRetainedContentKey, updateNodeElementChrome, renderNodeElementContent, buildActiveDragContext, positionFloatingOverlayWithinHost, hidePopover, legacyShowPopover, invokeAnnotationAction, renderNodeAnnotations, updateConnectorAnchorHover, getConnectorAnchorPoints, hideStatusNotice, showStatusNotice, renderEmptyStateOverlay, clearSnapGuides, legacyRenderSnapGuides, legacyRenderConnectorAnchorOverlay, getSelectionBounds, legacyRenderTransformHandlesOverlay, resolveSnapAdjustment, legacyRenderDebugDecorations, legacyBuildDiagnosticsSnapshot, renderDiagnosticsOverlay, navigateViaMinimap, resolveClipboardAnchor, buildClipboardPayload, copySelectionToClipboard, requestClipboardDuplicate, toggleMinimap, toggleDiagnostics, invalidateMeasuredLayout, legacyMeasureRenderedNodeSizes, legacyScheduleNodeMeasurement, getHostPoint, worldToHostPoint, getWorldPoint, hitTestNode, hitTestFrameHandle, hitTestProgressBadge, isOverlayTarget, applyFullTextTooltip, reconcileSelection, applySelection, selectSingleNode, publishSelection, clearViewportStateCommit, createSerializedStateSnapshot, invokeStateChanged, publishState, publishStateNow, scheduleViewportStateCommit, publishNodesMoved, setSelection, toggleSelection, toggleCollapse, clearContextMenu, closeComposer, ensureHostFocus, deferHostFocus, resolveComposerAnchor, layoutComposer, render, getContextActions, isCreateAction, buildCreateRequest, resolveMenuLabel, getMenuScale, isHiveLayout, isCompactHiveLayout, resolveMenuActionVariant, getActionMetrics, applyProgressPresetTone, fitContextMenuLabel, resolveActionGlyph, createMenuActionIcon, resolveMenuActionAriaLabel, getRadialOffsets, buildCompactHiveCoordinates, getCompactHiveOffsets, resolveContextMenuOffsets, resolveContextMenuSafeTop, getContextMenuLayerBounds, clampLayerBoundsToHost, positionContextMenu, getContextMenuOrbitRadius, getContextMenuLocalPoint, isPointInContextMenuLayer, closeContextMenuLayersFrom, syncContextMenuLayers, resolveSubmenuOrigin, ensureSubmenuLoadingIndicator, clearSubmenuLoadingIndicator, cancelPendingContextSubmenu, scheduleContextSubmenuOpen, clampLayerOriginToHost, getToolboxPanelSize, getToolboxPanelBounds, clampToolboxPanelOriginToHost, resolveToolboxPanelOrigin, createContextMenuLayer, syncContextMenuLayerShellGeometry, shiftContextMenuLayerOrigin, nudgeContextMenuLayerIntoVisibleHost, resolveQuickCreateSourceNode, submitCreateRequest, submitNodeEdit } = shared;
    function readFileAsUpload(file) {
        return new Promise((resolve, reject) => {
            if (!file) {
                resolve(null);
                return;
            }

            const reader = new FileReader();
            reader.onload = () => {
                const result = typeof reader.result === "string" ? reader.result : "";
                const separatorIndex = result.indexOf(",");
                resolve({
                    fileName: file.name || "upload.bin",
                    contentType: file.type || "application/octet-stream",
                    base64Data: separatorIndex >= 0 ? result.substring(separatorIndex + 1) : result
                });
            };
            reader.onerror = () => reject(reader.error || new Error("Failed to read file."));
            reader.readAsDataURL(file);
        });
    }

    function commitComposer(state) {
        if (!state.composer) {
            return;
        }

        if (state.composer.kind === "create") {
            if (state.composer.requiresFile && !state.composer.uploadedFile) {
                return;
            }

            const inputValues = Array.isArray(state.composer.inputFieldEntries)
                ? state.composer.inputFieldEntries.map(entry => ({
                    key: entry.key,
                    value: (entry.input.value || "").trim()
                }))
                : [];
            const hasMissingRequiredInputs = Array.isArray(state.composer.inputFieldEntries) &&
                state.composer.inputFieldEntries.some(entry => entry.isRequired && !(entry.input.value || "").trim());
            if (hasMissingRequiredInputs) {
                return;
            }

            submitCreateRequest(state, {
                ...state.composer.request,
                title: state.composer.titleInput ? state.composer.titleInput.value.trim() : "",
                subtitle: state.composer.subtitleInput ? state.composer.subtitleInput.value.trim() : "",
                notes: state.composer.notesInput ? state.composer.notesInput.value.trim() : "",
                objectSubtype: state.composer.request.objectSubtype || "",
                inputValues,
                uploadedFile: state.composer.uploadedFile || null
            }, { focusHost: true });
            closeComposer(state);
            return;
        }

        const text = state.composer.textInput.value.trim();
        if (!text) {
            return;
        }

        if (state.composer.kind === "note-create") {
            submitCreateRequest(state, {
                actionId: state.composer.actionId,
                sourceNodeId: state.composer.sourceNodeId,
                parentNodeId: state.composer.parentNodeId,
                x: round(state.composer.anchorWorld.x),
                y: round(state.composer.anchorWorld.y),
                title: text,
                subtitle: "",
                notes: text,
                placementKind: state.composer.placementKind,
                createMode: "quick-note"
            }, { focusHost: true });
        }
        else if (state.composer.kind === "note-edit") {
            submitNodeEdit(state, {
                nodeId: state.composer.nodeId,
                title: text,
                notes: text
            });
        }

        closeComposer(state);
    }

    function decorateComposerShell(state, title, kicker, variant) {
        const composer = createElement(state.document, "div", `cw-canvas-composer ${variant ? `is-${variant}` : ""}`);
        composer.addEventListener("pointerdown", event => event.stopPropagation());
        composer.addEventListener("wheel", event => {
            event.stopPropagation();
        }, { passive: true });
        composer.addEventListener("keydown", event => {
            if (event.key === "Escape") {
                event.preventDefault();
                closeComposer(state);
                return;
            }

            if (event.key === "Enter" && !event.shiftKey && !event.ctrlKey && !event.metaKey) {
                const tagName = event.target?.tagName?.toLowerCase?.() || "";
                if (variant === "note" || tagName !== "textarea") {
                    event.preventDefault();
                    commitComposer(state);
                }
            }
        });

        const card = createElement(state.document, "div", "cw-canvas-composer__card");
        if (kicker) {
            card.appendChild(createElement(state.document, "p", "cw-canvas-composer__kicker", kicker));
        }

        if (title) {
            card.appendChild(createElement(state.document, "h3", "cw-canvas-composer__title", title));
        }

        composer.appendChild(card);
        state.host.appendChild(composer);
        return { composer, card };
    }

    function createComposerWizard(document, steps) {
        if (!Array.isArray(steps) || steps.length === 0) {
            return null;
        }

        const wizard = createElement(document, "ol", "cw-canvas-composer__wizard");
        for (const step of steps) {
            const item = createElement(document, "li", "cw-canvas-composer__wizard-item");
            item.appendChild(createElement(document, "span", "cw-canvas-composer__wizard-step", `Step ${step.number}`));
            item.appendChild(createElement(document, "strong", "cw-canvas-composer__wizard-label", step.title));
            wizard.appendChild(item);
        }

        return wizard;
    }

    function createComposerSection(document, stepNumber, title, description) {
        const section = createElement(document, "section", "cw-canvas-composer__section");
        const header = createElement(document, "div", "cw-canvas-composer__section-header");
        header.appendChild(createElement(document, "span", "cw-canvas-composer__section-step", String(stepNumber).padStart(2, "0")));

        const copy = createElement(document, "div", "cw-canvas-composer__section-copy");
        copy.appendChild(createElement(document, "strong", null, title));
        if (description) {
            copy.appendChild(createElement(document, "small", null, description));
        }

        header.appendChild(copy);

        const body = createElement(document, "div", "cw-canvas-composer__section-body");
        section.appendChild(header);
        section.appendChild(body);
        return { section, body };
    }

    function updateComposerFileState(composer) {
        if (!composer) {
            return;
        }

        if (composer.fileSummary) {
            composer.fileSummary.textContent = composer.uploadedFile
                ? `${composer.uploadedFile.fileName} ready`
                : (composer.filePrompt || "Drop a file here or choose one.");
        }

        if (composer.createButton) {
            const hasMissingRequiredInputs = Array.isArray(composer.inputFieldEntries) &&
                composer.inputFieldEntries.some(entry => entry.isRequired && !(entry.input.value || "").trim());
            composer.createButton.disabled = (!!composer.requiresFile && !composer.uploadedFile) || hasMissingRequiredInputs;
        }
    }

    function openCreateComposer(state, action, request) {
        clearContextMenu(state);
        closeComposer(state, { focusHost: false });

        const shell = decorateComposerShell(state, `Create ${action.label || "item"}`, action.label || "Create", "dialog");
        const setupRendererKey = (action.setupRendererKey || "").trim().toLowerCase().replace(/[^a-z0-9_-]/g, "-");
        if (setupRendererKey) {
            shell.composer.dataset.setupRenderer = setupRendererKey;
            shell.composer.classList.add(`renderer-${setupRendererKey}`);
        }

        const dialogBody = createElement(state.document, "div", "cw-canvas-composer__dialog-body");
        const overview = createElement(state.document, "div", "cw-canvas-composer__overview");
        const scroll = createElement(state.document, "div", "cw-canvas-composer__scroll");
        const sectionSteps = [];
        const appendSection = (title, description) => {
            const stepNumber = sectionSteps.length + 1;
            sectionSteps.push({ number: stepNumber, title });
            const section = createComposerSection(state.document, stepNumber, title, description);
            scroll.appendChild(section.section);
            return section.body;
        };
        const showDefaultTextFields = action.showDefaultTextFields !== false;
        let titleInput = null;
        let subtitleInput = null;
        let notesInput = null;
        let defaultFields = null;
        let notesFields = null;

        if (action.description) {
            overview.appendChild(createElement(state.document, "p", "cw-canvas-composer__intro", action.description));
        }

        if (showDefaultTextFields) {
            const detailsSection = appendSection("Details", "Name the item and keep the create request readable on the canvas.");
            defaultFields = createElement(state.document, "div", "cw-canvas-composer__fields");
            detailsSection.appendChild(defaultFields);

            const titleField = createElement(state.document, "label", "cw-canvas-composer__field");
            titleField.appendChild(createElement(state.document, "span", null, action.titleLabel || "Title"));
            titleInput = createElement(state.document, "input", "cw-canvas-composer__input");
            titleInput.type = "text";
            titleInput.value = request?.title || "";
            titleInput.placeholder = action.titlePlaceholder || "";
            titleField.appendChild(titleInput);
            defaultFields.appendChild(titleField);

            const subtitleField = createElement(state.document, "label", "cw-canvas-composer__field");
            subtitleField.appendChild(createElement(state.document, "span", null, action.subtitleLabel || "Subtitle"));
            subtitleInput = createElement(state.document, "input", "cw-canvas-composer__input");
            subtitleInput.type = "text";
            subtitleInput.value = request?.subtitle || "";
            subtitleInput.placeholder = action.subtitlePlaceholder || "";
            subtitleField.appendChild(subtitleInput);
            defaultFields.appendChild(subtitleField);

            const notesField = createElement(state.document, "label", "cw-canvas-composer__field");
            notesField.appendChild(createElement(state.document, "span", null, action.notesLabel || "Notes"));
            notesInput = createElement(state.document, "textarea", "cw-canvas-composer__textarea");
            notesInput.value = request?.notes || "";
            notesInput.placeholder = action.notesPlaceholder || "";
            notesField.appendChild(notesInput);

            const notesSection = appendSection(
                "Notes",
                action.requiresFile
                    ? "Capture the supporting context after the required file is attached."
                    : "Capture the supporting context and next-step guidance for the new item.");
            notesFields = createElement(state.document, "div", "cw-canvas-composer__fields");
            notesFields.appendChild(notesField);
            notesSection.appendChild(notesFields);
        }

        const inputValueLookup = new Map();
        const initialInputValues = Array.isArray(request?.inputValues) && request.inputValues.length
            ? request.inputValues
            : (Array.isArray(action?.defaultInputValues) ? action.defaultInputValues : []);
        for (const item of initialInputValues) {
            if (!item?.key) {
                continue;
            }

            inputValueLookup.set(item.key, item.value || "");
        }

        const inputFieldEntries = [];
        const inputFieldSections = new Map();
        const resolveInputFieldContainer = field => {
            const sectionKey = field.sectionKey || "inputs";
            const existing = inputFieldSections.get(sectionKey);
            if (existing) {
                return existing;
            }

            const section = appendSection(
                field.sectionTitle || "Inputs",
                field.sectionDescription || "Complete the typed fields required before the item can be created.");
            const fields = createElement(state.document, "div", "cw-canvas-composer__fields");
            section.appendChild(fields);
            inputFieldSections.set(sectionKey, fields);
            return fields;
        };

        for (const field of action.inputFields || []) {
            const inputFields = resolveInputFieldContainer(field);
            const fieldWrapper = createElement(state.document, "label", "cw-canvas-composer__field");
            fieldWrapper.appendChild(createElement(
                state.document,
                "span",
                null,
                `${field.label || field.key || "Value"}${field.isRequired ? " *" : ""}`));

            const inputMode = (field.inputMode || "text").toLowerCase();
            const isMultiline = inputMode === "textarea" || inputMode === "multiline";
            const isSelect = inputMode === "select";
            const input = createElement(
                state.document,
                isSelect ? "select" : (isMultiline ? "textarea" : "input"),
                isMultiline ? "cw-canvas-composer__textarea" : "cw-canvas-composer__input");
            if (isSelect) {
                const placeholderValue = field.placeholder || "Select an option";
                const placeholderOption = createElement(state.document, "option", null, placeholderValue);
                placeholderOption.value = "";
                input.appendChild(placeholderOption);
                for (const option of field.options || []) {
                    const optionElement = createElement(state.document, "option", null, option.label || option.value || "");
                    optionElement.value = option.value || "";
                    input.appendChild(optionElement);
                }
            }
            else if (!isMultiline) {
                input.type = inputMode === "url" ? "url" :
                    inputMode === "date" ? "date" :
                        inputMode === "datetime-local" ? "datetime-local" :
                            inputMode === "number" ? "number" :
                                inputMode === "email" ? "email" :
                                    inputMode === "tel" ? "tel" :
                                        "text";
            }

            let inputValue = inputValueLookup.get(field.key) || "";
            if (isSelect && !inputValue && field.isRequired) {
                const selectableOptions = Array.from(input.options).filter(option => !!option.value);
                if (selectableOptions.length === 1) {
                    inputValue = selectableOptions[0].value;
                }
            }

            input.value = inputValue;
            if (!isSelect) {
                input.placeholder = field.placeholder || "";
            }
            fieldWrapper.appendChild(input);
            inputFields.appendChild(fieldWrapper);
            const notifyInputChanged = () => {
                if (state.composer) {
                    updateComposerFileState(state.composer);
                }
            };
            input.addEventListener("input", notifyInputChanged);
            input.addEventListener("change", notifyInputChanged);
            inputFieldEntries.push({
                key: field.key,
                input,
                isRequired: !!field.isRequired
            });
        }

        let uploadedFile = request?.uploadedFile || null;
        let fileSummary = null;
        let fileInput = null;
        if (action.requiresFile) {
            const uploadSection = appendSection(
                "Attachment",
                action.supportsDragDrop
                    ? "Drop the required file here or choose it from disk."
                    : "Choose the required file from disk before you create the item.");
            const uploadField = createElement(state.document, "div", "cw-canvas-composer__upload");
            const uploadTitle = createElement(state.document, "span", "cw-canvas-composer__upload-title", action.filePrompt || "Drop a file here or choose one.");
            uploadField.appendChild(uploadTitle);

            const dropZone = createElement(state.document, "div", "cw-canvas-composer__dropzone");
            dropZone.tabIndex = 0;
            fileInput = createElement(state.document, "input", "cw-canvas-composer__file-input");
            fileInput.type = "file";
            fileInput.tabIndex = -1;
            fileInput.setAttribute("aria-hidden", "true");
            if (action.acceptedFileTypes) {
                fileInput.accept = action.acceptedFileTypes;
            }

            const fileTrigger = createElement(state.document, "button", "cw-button cw-canvas-composer__file-trigger", "Choose file");
            fileTrigger.type = "button";
            fileTrigger.dataset.tone = "ghost";
            const dropCopy = createElement(state.document, "span", null, action.supportsDragDrop ? "Click or drop a file here." : "Click to choose a file.");
            fileSummary = createElement(state.document, "small", "cw-canvas-composer__upload-summary", "");
            dropZone.appendChild(fileInput);
            dropZone.appendChild(fileTrigger);
            dropZone.appendChild(dropCopy);
            dropZone.appendChild(fileSummary);
            uploadField.appendChild(dropZone);
            uploadSection.appendChild(uploadField);

            const assignUpload = async file => {
                uploadedFile = await readFileAsUpload(file);
                if (state.composer) {
                    state.composer.uploadedFile = uploadedFile;
                    updateComposerFileState(state.composer);
                }
            };

            const openFilePicker = event => {
                event?.preventDefault?.();
                event?.stopPropagation?.();
                if (!fileInput) {
                    return;
                }

                try {
                    if (typeof fileInput.showPicker === "function") {
                        fileInput.showPicker();
                        return;
                    }
                }
                catch {
                }

                fileInput.click();
            };

            dropZone.addEventListener("pointerdown", event => event.stopPropagation());
            dropZone.addEventListener("click", event => {
                if (event.target === fileInput || event.target?.closest?.(".cw-canvas-composer__file-trigger")) {
                    return;
                }

                openFilePicker(event);
            });
            dropZone.addEventListener("keydown", event => {
                if (event.key !== "Enter" && event.key !== " ") {
                    return;
                }

                openFilePicker(event);
            });
            fileTrigger.addEventListener("pointerdown", event => event.stopPropagation());
            fileTrigger.addEventListener("click", openFilePicker);
            fileInput.addEventListener("click", event => event.stopPropagation());
            fileInput.addEventListener("change", async event => {
                const file = event.target?.files?.[0];
                if (!file) {
                    return;
                }

                await assignUpload(file);
            });

            if (action.supportsDragDrop) {
                ["dragenter", "dragover"].forEach(eventName => dropZone.addEventListener(eventName, event => {
                    event.preventDefault();
                    dropZone.classList.add("is-dragover");
                }));
                ["dragleave", "dragend"].forEach(eventName => dropZone.addEventListener(eventName, () => {
                    dropZone.classList.remove("is-dragover");
                }));
                dropZone.addEventListener("drop", async event => {
                    event.preventDefault();
                    dropZone.classList.remove("is-dragover");
                    const file = event.dataTransfer?.files?.[0];
                    if (!file) {
                        return;
                    }

                    await assignUpload(file);
                });
            }
        }

        const wizard = createComposerWizard(state.document, sectionSteps);
        if (wizard) {
            overview.appendChild(wizard);
        }

        const actions = createElement(state.document, "div", "cw-canvas-composer__actions");
        const cancel = createElement(state.document, "button", "cw-button");
        cancel.type = "button";
        cancel.textContent = "Cancel";
        cancel.addEventListener("click", () => closeComposer(state));
        actions.appendChild(cancel);

        const create = createElement(state.document, "button", "cw-button");
        create.type = "button";
        create.dataset.tone = "accent";
        create.textContent = action.submitLabel || action.label || "Create";
        create.addEventListener("click", () => commitComposer(state));
        actions.appendChild(create);

        if (overview.childElementCount > 0) {
            dialogBody.appendChild(overview);
        }
        dialogBody.appendChild(scroll);
        shell.card.appendChild(dialogBody);
        shell.card.appendChild(actions);

        state.composer = {
            kind: "create",
            element: shell.composer,
            request: request || {},
            anchorWorld: request ? { x: request.x || 0, y: request.y || 0 } : { x: 0, y: 0 },
            titleInput,
            subtitleInput,
            notesInput,
            inputFieldEntries,
            createButton: create,
            requiresFile: !!action.requiresFile,
            uploadedFile,
            fileSummary,
            fileInput,
            filePrompt: action.filePrompt || "Drop a file here or choose one."
        };

        window.requestAnimationFrame(() => {
            layoutComposer(state);
            updateComposerFileState(state.composer);
            const firstInput = inputFieldEntries[0]?.input || titleInput || subtitleInput || notesInput;
            if (firstInput) {
                firstInput.focus();
                if (typeof firstInput.select === "function") {
                    firstInput.select();
                }
            }
        });
    }

    function openInlineNoteComposer(state, options) {
        closeComposer(state, { focusHost: false });
        clearContextMenu(state);

        const shell = decorateComposerShell(state, "", "", "note");
        const noteEditor = createElement(state.document, "div", "cw-note-editor");
        const textInput = createElement(state.document, "textarea", "cw-note-editor__input");
        textInput.value = options.value || "";
        textInput.placeholder = options.placeholder || state.surface.chrome.inlineNotePlaceholder || "Write note";
        noteEditor.appendChild(textInput);
        noteEditor.appendChild(createElement(state.document, "p", "cw-note-editor__hint", "Enter saves. Shift+Enter adds a line. Escape cancels."));
        shell.card.appendChild(noteEditor);

        state.composer = {
            kind: options.kind,
            element: shell.composer,
            actionId: options.actionId || "",
            sourceNodeId: options.sourceNodeId || null,
            parentNodeId: options.parentNodeId || null,
            placementKind: options.placementKind || "child",
            nodeId: options.nodeId || null,
            anchorWorld: options.anchorWorld,
            textInput
        };

        window.requestAnimationFrame(() => {
            layoutComposer(state);
            textInput.focus();
            textInput.select();
        });
    }

    function resolveChildNoteHorizontalDirection(state, node) {
        const parent = node?.parentId ? state.lookups.byId.get(node.parentId) : null;
        if (!node || !parent) {
            return 1;
        }

        const nodePosition = getNodePosition(state, node);
        const parentPosition = getNodePosition(state, parent);
        return nodePosition.x < parentPosition.x ? -1 : 1;
    }

    function buildChildNotePlacement(position, childCount, sourceSize, horizontalDirection) {
        const column = childCount % 3;
        const row = Math.floor(childCount / 3);
        const horizontalGap = ((sourceSize?.width || 148) / 2) + 98;
        const direction = horizontalDirection < 0 ? -1 : 1;
        return {
            x: round(position.x + (direction * (horizontalGap + (column * 36)))),
            y: round(position.y + (row * 104))
        };
    }

    function buildSiblingNotePlacement(position, siblingCount, sourceSize) {
        const verticalGap = ((sourceSize?.height || 76) / 2) + 58;
        return {
            x: round(position.x + ((siblingCount % 2) * 24)),
            y: round(position.y + verticalGap)
        };
    }

    function openKeyboardNoteComposer(state, placementKind) {
        const selectedId = state.ui.selectedNodeIds[0];
        if (!selectedId) {
            return;
        }

        const node = state.lookups.byId.get(selectedId);
        if (!node) {
            return;
        }

        const isSibling = placementKind === "sibling";
        const actionId = isSibling ? state.surface.chrome.siblingNoteActionId : state.surface.chrome.childNoteActionId;
        if (!actionId) {
            return;
        }

        const position = getNodePosition(state, node);
        const sourceSize = getNodeSize(state, node);
        const anchorWorld = isSibling
            ? buildSiblingNotePlacement(position, state.surface.nodes.filter(candidate => candidate.parentId === node.parentId && candidate.id !== node.id).length, sourceSize)
            : buildChildNotePlacement(position, state.surface.nodes.filter(candidate => candidate.parentId === node.id).length, sourceSize, resolveChildNoteHorizontalDirection(state, node));

        openInlineNoteComposer(state, {
            kind: "note-create",
            actionId,
            sourceNodeId: node.id,
            parentNodeId: isSibling ? (node.parentId || node.id) : node.id,
            placementKind,
            anchorWorld,
            value: "",
            placeholder: node.inlineTextPlaceholder || state.surface.chrome.inlineNotePlaceholder || "Write note"
        });
    }

    function openExistingNoteEditor(state, node) {
        openInlineNoteComposer(state, {
            kind: "note-edit",
            nodeId: node.id,
            anchorWorld: getNodePosition(state, node),
            value: node.inlineText || node.title || "",
            placeholder: node.inlineTextPlaceholder || state.surface.chrome.inlineNotePlaceholder || "Write note"
        });
    }

    function executeContextAction(state, node, action, clientX, clientY, placementKind) {
        if (action?.children?.length) {
            return;
        }

        if (isCreateAction(action) || !node) {
            const request = buildCreateRequest(
                state,
                action,
                node,
                node ? getNodePosition(state, node) : getWorldPoint(state, clientX, clientY),
                placementKind || (node ? "child" : "canvas"));

            if (action.requiresInput) {
                openCreateComposer(state, action, request);
                return;
            }

            submitCreateRequest(state, request, { focusHost: true });
            clearContextMenu(state);
            return;
        }

        clearContextMenu(state);
        const position = getNodePosition(state, node);
        state.dotNetRef.invokeMethodAsync("OnContextAction", node.id, action.actionId, round(position.x), round(position.y));
    }

    function openContextSubmenu(state, parentLayer, options, action, offset) {
        cancelPendingContextSubmenu(state);
        const nextDepth = parentLayer.depth + 1;
        const existingLayer = state.contextMenuState?.layers?.[nextDepth];
        if (existingLayer &&
            existingLayer.ownerActionId === action.actionId &&
            existingLayer.ownerDepth === parentLayer.depth) {
            return;
        }

        closeContextMenuLayersFrom(state, nextDepth);

        const submenuLayout = action.submenuLayout || "compact-hive";
        if (submenuLayout === "toolbox-panel") {
            const panelSize = getToolboxPanelSize();
            const submenuOrigin = clampToolboxPanelOriginToHost(
                state,
                resolveToolboxPanelOrigin(parentLayer, offset, panelSize),
                panelSize);
            const submenuLayer = createContextMenuLayer(state, {
                depth: nextDepth,
                label: action.label || "Components",
                originOffset: submenuOrigin,
                ownerActionId: action.actionId,
                ownerDepth: parentLayer.depth,
                mode: "panel",
                panelSize
            });

            renderToolboxPanelLayer(state, submenuLayer, {
                actions: action.children || [],
                node: options.node,
                clientX: options.clientX,
                clientY: options.clientY,
                placementKind: options.placementKind,
                label: action.label || "Components",
                description: action.description || ""
            });

            state.contextMenu.appendChild(submenuLayer.element);
            state.contextMenuState.layers.push(submenuLayer);
            nudgeContextMenuLayerIntoVisibleHost(state, submenuLayer);
            return;
        }

        const menuScale = getMenuScale(state);
        const submenuBaseRadius = (isHiveLayout(submenuLayout) ? 74 : 80) * menuScale;
        const submenuRingStep = (isHiveLayout(submenuLayout) ? 0 : 64) * menuScale;
        const submenuOffsets = resolveContextMenuOffsets(state, action.children || [], submenuBaseRadius, submenuRingStep, submenuLayout);
        const submenuRadius = getContextMenuOrbitRadius(state, submenuOffsets, action.children || []);
        const submenuOrigin = clampLayerOriginToHost(
            state,
            resolveSubmenuOrigin(parentLayer, offset, submenuLayout),
            submenuOffsets,
            submenuRadius,
            action.children || [],
            submenuLayout);

        const submenuLayer = createContextMenuLayer(state, {
            depth: nextDepth,
            label: action.label || "More",
            originOffset: submenuOrigin,
            layout: submenuLayout,
            ownerActionId: action.actionId,
            ownerDepth: parentLayer.depth
        });

        renderContextMenuLayer(state, submenuLayer, {
            actions: action.children || [],
            offsets: submenuOffsets,
            node: options.node,
            clientX: options.clientX,
            clientY: options.clientY,
            placementKind: options.placementKind,
            depth: nextDepth,
            baseRadius: submenuBaseRadius,
            ringStep: submenuRingStep,
            submenuLayout
        });

        state.contextMenu.appendChild(submenuLayer.element);
        state.contextMenuState.layers.push(submenuLayer);
        nudgeContextMenuLayerIntoVisibleHost(state, submenuLayer);
    }

    function openContextSubmenuByActionId(state, actionId) {
        const rootLayer = state.contextMenuState?.layers?.[0];
        const entry = rootLayer?.actionEntries?.get(actionId || "");
        if (!entry || !entry.action?.children?.length) {
            return false;
        }

        openContextSubmenu(state, rootLayer, entry.options, entry.action, entry.offset);
        return true;
    }

    function renderContextMenuLayer(state, layerState, options) {
        const layout = options.submenuLayout || "";
        const offsets = options.offsets || resolveContextMenuOffsets(state, options.actions, options.baseRadius, options.ringStep, layout);
        layerState.actionEntries = new Map();
        layerState.layout = layout;
        layerState.radius = getContextMenuOrbitRadius(state, offsets, options.actions);
        layerState.bounds = getContextMenuLayerBounds(state, layerState.originOffset, offsets, layerState.radius, options.actions, layout);
        syncContextMenuLayerShellGeometry(layerState);
        options.actions.forEach((action, index) => {
            const offset = offsets[index];
            const variant = resolveMenuActionVariant(action);
            const button = createElement(
                state.document,
                "button",
                `cw-context-menu__action tone-${action.tone || "neutral"}${(action.menuSize || "").toLowerCase() === "compact" ? " is-compact" : ""}`);
            button.type = "button";
            button.dataset.actionId = action.actionId || "";
            button.dataset.layerDepth = `${options.depth || 0}`;
            button.dataset.menuSize = action.menuSize || "normal";
            button.dataset.shortcutKey = shared.getMenuActionShortcutKey?.(action) || "";
            button.title = action.description || resolveMenuActionAriaLabel(action);
            button.setAttribute("aria-label", resolveMenuActionAriaLabel(action));
            if (button.dataset.shortcutKey) {
                button.setAttribute("aria-keyshortcuts", button.dataset.shortcutKey.toUpperCase());
            }
            button.style.setProperty("--cw-menu-x", `${round(offset.x)}px`);
            button.style.setProperty("--cw-menu-y", `${round(offset.y)}px`);
            if (variant === "progress-preset") {
                button.classList.add("is-progress-preset");
                applyProgressPresetTone(button, action);
            }
            else if (variant === "marker-preset") {
                button.classList.add("is-marker-preset");
            }
            else if (variant === "priority-preset") {
                button.classList.add("is-priority-preset");
            }
            button.addEventListener("pointerdown", event => event.stopPropagation());
            button.addEventListener("pointermove", event => {
                event.stopPropagation();
            });
            button.addEventListener("pointerenter", () => {
                if (!action.children?.length) {
                    cancelPendingContextSubmenu(state);
                    closeContextMenuLayersFrom(state, (options.depth || 0) + 1);
                    return;
                }

                scheduleContextSubmenuOpen(state, layerState, options, action, offset, button);
            });
            button.addEventListener("pointerleave", () => {
                cancelPendingContextSubmenu(
                    state,
                    pending => pending.ownerActionId === (action.actionId || "") && pending.ownerDepth === layerState.depth);
            });
            button.addEventListener("click", event => {
                event.stopPropagation();
                executeContextAction(state, options.node, action, options.clientX, options.clientY, options.placementKind);
            });

            button.appendChild(createMenuActionIcon(state, action));
            let label = null;
            if (variant !== "priority-preset") {
                label = typeof shared.buildContextMenuActionLabel === "function"
                    ? shared.buildContextMenuActionLabel(state, action)
                    : createElement(state.document, "strong", "cw-context-menu__label", resolveMenuLabel(action));
                if (!label.dataset.fullText) {
                    label.dataset.fullText = resolveMenuLabel(action);
                }
                button.appendChild(label);
            }
            if (action.children?.length) {
                button.appendChild(createElement(state.document, "span", "cw-context-menu__caret", "\u203A"));
                button.classList.add("has-children");
            }

            layerState.orbit.appendChild(button);
            if (label) {
                fitContextMenuLabel(button, label, variant);
            }
            layerState.actionEntries.set(action.actionId || `index-${index}`, {
                action,
                offset,
                button,
                options: {
                    node: options.node,
                    clientX: options.clientX,
                    clientY: options.clientY,
                    placementKind: options.placementKind
                }
            });
        });

        return offsets;
    }

    function renderToolboxPreview(state, layerState, previewHost, action, sectionLabel, groupLabel) {
        if (!previewHost) {
            return;
        }

        clear(previewHost);

        const kicker = createElement(state.document, "span", "cw-context-toolbox__preview-kicker", `${sectionLabel} / ${groupLabel}`);
        const title = createElement(state.document, "strong", "cw-context-toolbox__preview-title", action?.label || action?.menuLabel || "Component");
        const copy = createElement(
            state.document,
            "p",
            "cw-context-toolbox__preview-copy",
            action?.description || "Hover a component to preview its prompt text and usage.");
        const meta = createElement(state.document, "div", "cw-context-toolbox__preview-meta");
        meta.appendChild(createElement(
            state.document,
            "span",
            "cw-context-toolbox__preview-pill",
            action?.requiresInput ? `${action.inputFields?.length || 0} inputs required` : "Ready to add"));
        meta.appendChild(createElement(
            state.document,
            "span",
            "cw-context-toolbox__preview-pill",
            action?.tone || "neutral"));

        previewHost.appendChild(kicker);
        previewHost.appendChild(title);
        previewHost.appendChild(copy);
        previewHost.appendChild(meta);
        layerState.previewActionId = action?.actionId || "";
    }

    function renderToolboxPanelLayer(state, layerState, options) {
        const panel = layerState.panel;
        if (!panel) {
            return;
        }

        clear(panel);
        layerState.actionEntries = new Map();

        const header = createElement(state.document, "div", "cw-context-toolbox__header");
        const headerCopy = createElement(state.document, "div", "cw-context-toolbox__header-copy");
        headerCopy.appendChild(createElement(state.document, "span", "cw-context-toolbox__eyebrow", options.label || "Components"));
        headerCopy.appendChild(createElement(state.document, "strong", "cw-context-toolbox__title", options.label || "Prompt components"));
        if (options.description) {
            headerCopy.appendChild(createElement(state.document, "p", "cw-context-toolbox__copy", options.description));
        }
        header.appendChild(headerCopy);
        header.appendChild(createElement(state.document, "span", "cw-context-toolbox__count", `${options.actions?.length || 0} sections`));
        panel.appendChild(header);

        const search = createElement(state.document, "input", "cw-context-toolbox__search");
        search.type = "search";
        search.placeholder = "Search components";
        search.setAttribute("aria-label", "Search prompt components");
        panel.appendChild(search);

        const body = createElement(state.document, "div", "cw-context-toolbox__body");
        const sectionsHost = createElement(state.document, "div", "cw-context-toolbox__sections");
        const previewHost = createElement(state.document, "aside", "cw-context-toolbox__preview");
        body.appendChild(sectionsHost);
        body.appendChild(previewHost);
        panel.appendChild(body);

        const renderSections = () => {
            clear(sectionsHost);
            const query = (search.value || "").trim().toLowerCase();
            let firstPreview = null;

            for (let sectionIndex = 0; sectionIndex < (options.actions || []).length; sectionIndex++) {
                const sectionAction = options.actions[sectionIndex];
                const matchingGroups = [];

                for (const groupAction of (sectionAction.children || [])) {
                    const matchingItems = (groupAction.children || []).filter(item => {
                        if (!query) {
                            return true;
                        }

                        const haystack = [
                            item.label,
                            item.menuLabel,
                            item.description,
                            groupAction.label,
                            sectionAction.label
                        ].join(" ").toLowerCase();
                        return haystack.includes(query);
                    });

                    if (matchingItems.length > 0) {
                        matchingGroups.push({ groupAction, matchingItems });
                    }
                }

                const sectionMatches = !query ||
                    [sectionAction.label, sectionAction.description].join(" ").toLowerCase().includes(query);
                if (!sectionMatches && matchingGroups.length === 0) {
                    continue;
                }

                const section = createElement(state.document, "details", "cw-context-toolbox__section");
                section.open = !!query || sectionIndex === 0;

                const sectionSummary = createElement(state.document, "summary", "cw-context-toolbox__section-summary");
                const sectionBadge = createElement(state.document, `span`, `cw-context-toolbox__section-badge tone-${sectionAction.tone || "neutral"}`);
                sectionBadge.textContent = resolveActionGlyph(sectionAction.icon || "");
                const sectionSummaryCopy = createElement(state.document, "span", "cw-context-toolbox__section-copy");
                sectionSummaryCopy.appendChild(createElement(state.document, "strong", null, sectionAction.label || "Section"));
                sectionSummaryCopy.appendChild(createElement(state.document, "small", null, sectionAction.description || ""));
                sectionSummary.appendChild(sectionBadge);
                sectionSummary.appendChild(sectionSummaryCopy);
                section.appendChild(sectionSummary);

                const sectionBody = createElement(state.document, "div", "cw-context-toolbox__section-body");

                for (let groupIndex = 0; groupIndex < matchingGroups.length; groupIndex++) {
                    const groupEntry = matchingGroups[groupIndex];
                    const group = createElement(state.document, "details", "cw-context-toolbox__group");
                    group.open = !!query || groupIndex === 0;

                    const groupSummary = createElement(state.document, "summary", "cw-context-toolbox__group-summary");
                    groupSummary.appendChild(createElement(state.document, "strong", null, groupEntry.groupAction.label || "Group"));
                    groupSummary.appendChild(createElement(state.document, "small", null, groupEntry.groupAction.description || ""));
                    group.appendChild(groupSummary);

                    const itemList = createElement(state.document, "div", "cw-context-toolbox__item-list");
                    for (const itemAction of groupEntry.matchingItems) {
                        if (!firstPreview) {
                            firstPreview = {
                                action: itemAction,
                                sectionLabel: sectionAction.label || "Section",
                                groupLabel: groupEntry.groupAction.label || "Group"
                            };
                        }

                        const item = createElement(state.document, "button", "cw-context-toolbox__item");
                        item.type = "button";
                        item.dataset.tone = itemAction.tone || "neutral";
                        item.addEventListener("pointerdown", event => event.stopPropagation());
                        item.addEventListener("pointerenter", () => {
                            renderToolboxPreview(state, layerState, previewHost, itemAction, sectionAction.label || "Section", groupEntry.groupAction.label || "Group");
                        });
                        item.addEventListener("focus", () => {
                            renderToolboxPreview(state, layerState, previewHost, itemAction, sectionAction.label || "Section", groupEntry.groupAction.label || "Group");
                        });
                        item.addEventListener("click", event => {
                            event.stopPropagation();
                            executeContextAction(state, options.node, itemAction, options.clientX, options.clientY, options.placementKind);
                        });

                        const itemIcon = createElement(state.document, "span", "cw-context-toolbox__item-icon");
                        itemIcon.appendChild(createMenuActionIcon(state, itemAction));
                        const itemBody = createElement(state.document, "span", "cw-context-toolbox__item-body");
                        itemBody.appendChild(createElement(state.document, "strong", null, itemAction.label || itemAction.menuLabel || "Item"));
                        itemBody.appendChild(createElement(state.document, "small", null, itemAction.requiresInput ? "Specify inputs before inserting" : "Add directly to the prompt flow"));
                        item.appendChild(itemIcon);
                        item.appendChild(itemBody);
                        itemList.appendChild(item);
                    }

                    group.appendChild(itemList);
                    sectionBody.appendChild(group);
                }

                section.appendChild(sectionBody);
                sectionsHost.appendChild(section);
            }

            if (!sectionsHost.childElementCount) {
                sectionsHost.appendChild(createElement(state.document, "div", "cw-context-toolbox__empty", "No prompt components match the current search."));
                renderToolboxPreview(state, layerState, previewHost, null, "Search", "No results");
                return;
            }

            if (firstPreview && layerState.previewActionId !== firstPreview.action.actionId) {
                renderToolboxPreview(state, layerState, previewHost, firstPreview.action, firstPreview.sectionLabel, firstPreview.groupLabel);
            }
        };

        search.addEventListener("input", renderSections);
        renderSections();
        window.requestAnimationFrame(() => search.focus({ preventScroll: true }));
    }

    function showContextMenu(state, options) {
        clearContextMenu(state);
        const actions = options.actions || getContextActions(state, options.node);
        if (!actions.length) {
            return;
        }

        ensureHostFocus(state);
        deferHostFocus(state);
        const hostPoint = getHostPoint(state, options.clientX, options.clientY);
        state.contextMenu.style.display = "block";
        const menuScale = getMenuScale(state);
        const rootLayout = options.layout || "hive";
        const rootOffsets = resolveContextMenuOffsets(state, actions, 84 * menuScale, 62 * menuScale, rootLayout);
        const rootCenter = positionContextMenu(state, hostPoint, rootOffsets, actions, rootLayout);
        state.contextMenuState = {
            node: options.node || null,
            actions,
            clientX: options.clientX,
            clientY: options.clientY,
            placementKind: options.placementKind || (options.node ? "child" : "canvas"),
            rootCenter,
            layers: [],
            pendingSubmenu: null
        };

        const rootLayer = createContextMenuLayer(state, {
            depth: 0,
            label: options.label || options.node?.title || "Canvas",
            originOffset: { x: 0, y: 0 },
            layout: rootLayout
        });
        renderContextMenuLayer(state, rootLayer, {
            actions,
            node: options.node || null,
            clientX: options.clientX,
            clientY: options.clientY,
            placementKind: options.placementKind || (options.node ? "child" : "canvas"),
            depth: 0,
            baseRadius: 84 * menuScale,
            ringStep: 62 * menuScale,
            submenuLayout: rootLayout
        });
        state.contextMenu.appendChild(rootLayer.element);
        state.contextMenuState.layers.push(rootLayer);
    }

    function legacyOpenNodeMetadataMenu(state, node, actionId, anchorElement) {
        if (!node || !anchorElement) {
            return;
        }

        if (state.selectedIds.size !== 1 || !state.selectedIds.has(node.id)) {
            setSelection(state, [node.id], true);
        }

        const rect = anchorElement.getBoundingClientRect();
        showContextMenu(state, {
            node,
            clientX: rect.left + (rect.width / 2),
            clientY: rect.top + (rect.height / 2),
            placementKind: "child",
            label: node.title || "Canvas"
        });
        openContextSubmenuByActionId(state, actionId);
    }

    Object.assign(shared, { readFileAsUpload, commitComposer, decorateComposerShell, createComposerWizard, createComposerSection, updateComposerFileState, openCreateComposer, openInlineNoteComposer, buildChildNotePlacement, buildSiblingNotePlacement, openKeyboardNoteComposer, openExistingNoteEditor, executeContextAction, openContextSubmenu, openContextSubmenuByActionId, renderContextMenuLayer, renderToolboxPreview, renderToolboxPanelLayer, showContextMenu, legacyOpenNodeMetadataMenu });
})();
