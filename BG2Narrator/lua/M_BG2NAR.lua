-- EE only auto-loads M_*.lua when the basename is 8 characters or fewer.
if not EEex_Active then
	error("[BG2Narrator] EEex is not active. Start the game with InfinityLoader.exe / EEex.exe.")
end

print("[BG2Narrator] M_BG2NAR.lua loaded")
print(string.format(
	"[BG2Narrator] ipc probe: io.open=%s Infinity_SetINIValue=%s",
	tostring(io ~= nil and io.open ~= nil),
	tostring(Infinity_SetINIValue ~= nil)
))

-- Drop globals left by older BG2Narrator versions in the same EEex session.
BG2Narrator_OnAnyUiItemRender = nil
BG2Narrator_OnAnyListItemRender = nil

BG2Narrator = BG2Narrator or {}
BG2Narrator.version = "0.1.1"
BG2Narrator.logPath = BG2Narrator.logPath or "BG2Narrator.log"
BG2Narrator.eventsPath = BG2Narrator.eventsPath or "BG2Narrator/events.jsonl"

BG2Narrator.logAllActions = BG2Narrator.logAllActions or false
BG2Narrator.debugUiProbe = BG2Narrator.debugUiProbe or false
BG2Narrator.debugChoices = BG2Narrator.debugChoices or false
BG2Narrator.uiProbeSeconds = BG2Narrator.uiProbeSeconds or 5

BG2Narrator.dialogActionIds = BG2Narrator.dialogActionIds or {
	[137] = true,
	[138] = true,
	[139] = true,
}

BG2Narrator.playSoundThroughVoiceActionId = 471

BG2Narrator.uiProbeUntil = BG2Narrator.uiProbeUntil or 0
BG2Narrator.uiProbeSeen = BG2Narrator.uiProbeSeen or {}
BG2Narrator.uiProbeDebugSeen = BG2Narrator.uiProbeDebugSeen or {}
BG2Narrator.seenDialogueChunks = BG2Narrator.seenDialogueChunks or {}
BG2Narrator.lastDialogSpeaker = BG2Narrator.lastDialogSpeaker or ""
BG2Narrator.lastDialogTarget = BG2Narrator.lastDialogTarget or ""
BG2Narrator.dialogueGeneration = BG2Narrator.dialogueGeneration or 0
BG2Narrator.pendingSpeakId = BG2Narrator.pendingSpeakId or ""
BG2Narrator.voObserverUntil = BG2Narrator.voObserverUntil or 0
BG2Narrator.ipcSeq = BG2Narrator.ipcSeq or 0
BG2Narrator.ipcProfileSection = BG2Narrator.ipcProfileSection or "BG2Narrator"

local function canUseIo()
	return io ~= nil and io.open ~= nil
end

local function escapeIniValue(value)
	local text = tostring(value)
	text = text:gsub("%%", "%%%%")
	return text:gsub("\\", function(char)
		return "%" .. string.byte(char)
	end)
end

local function safeString(value)
	if value == nil then
		return ""
	end

	local ok, result = pcall(function()
		if value.m_pchData ~= nil then
			return value.m_pchData:get()
		end
		if value.get then
			return value:get()
		end
		return tostring(value)
	end)

	if ok and result ~= nil then
		return tostring(result)
	end

	return ""
end

local function formatLogTimestamp()
	if Infinity_GetClockTicks then
		return tostring(math.floor(Infinity_GetClockTicks())) .. "ms"
	end

	if os and os.date then
		return os.date("%Y-%m-%d %H:%M:%S")
	end

	return "t0"
end

function BG2Narrator.log(message)
	local line = string.format("[%s] %s", formatLogTimestamp(), message)
	print("[BG2Narrator] " .. message)

	pcall(function()
		local file = io.open(BG2Narrator.logPath, "a")
		if file then
			file:write(line .. "\n")
			file:close()
		end
	end)
end

local function nowSeconds()
	if Infinity_GetClockTicks then
		return Infinity_GetClockTicks() / 1000
	end

	if os and os.time then
		return os.time()
	end

	return 0
end

local function nowMillis()
	if Infinity_GetClockTicks then
		return math.floor(Infinity_GetClockTicks())
	end

	if os and os.time then
		return os.time() * 1000
	end

	return 0
end

local function jsonEscape(value)
	if value == nil then
		return ""
	end

	local text = tostring(value)
	text = text:gsub("\\", "\\\\")
	text = text:gsub("\"", "\\\"")
	text = text:gsub("\r", "\\r")
	text = text:gsub("\n", "\\n")
	return text
end

local function ensureEventsDirectory()
	local dir = BG2Narrator.eventsPath:match("^(.*)[/\\][^/\\]+$")
	if dir == nil or dir == "" then
		return
	end

	pcall(function()
		if Infinity_CreateDirectory then
			Infinity_CreateDirectory(dir)
		end
	end)
end

local function encodeEventJson(payload)
	local parts = {}
	for key, value in pairs(payload) do
		local encoded
		if type(value) == "number" then
			encoded = tostring(value)
		elseif type(value) == "boolean" then
			encoded = value and "true" or "false"
		else
			encoded = "\"" .. jsonEscape(value) .. "\""
		end
		table.insert(parts, "\"" .. jsonEscape(key) .. "\":" .. encoded)
	end

	return "{" .. table.concat(parts, ",") .. "}"
end

local function emitEventViaJsonl(line)
	ensureEventsDirectory()

	local ok, wrote = pcall(function()
		local file = io.open(BG2Narrator.eventsPath, "a")
		if file then
			file:write(line .. "\n")
			file:close()
			return true
		end
		return false
	end)

	if not ok then
		BG2Narrator.log("ipc-jsonl-error " .. tostring(wrote))
		return false
	end

	return wrote == true
end

local function emitEventViaBaldurIni(line)
	if Infinity_SetINIValue == nil then
		return false
	end

	local ok, err = pcall(function()
		BG2Narrator.ipcSeq = BG2Narrator.ipcSeq + 1
		local slot = "Event" .. (BG2Narrator.ipcSeq % 8)
		Infinity_SetINIValue(BG2Narrator.ipcProfileSection, slot, escapeIniValue(line))
		Infinity_SetINIValue(BG2Narrator.ipcProfileSection, "LastSeq", tostring(BG2Narrator.ipcSeq))
	end)

	if not ok then
		BG2Narrator.log("ipc-baldur-error " .. tostring(err))
		return false
	end

	return true
end

function BG2Narrator.emitEvent(payload)
	local line = encodeEventJson(payload)

	if canUseIo() and emitEventViaJsonl(line) then
		return
	end

	if emitEventViaBaldurIni(line) then
		return
	end

	BG2Narrator.log("ipc-error no working backend (io and Baldur.lua IPC unavailable)")
end

function BG2Narrator.emitStop(reason)
	BG2Narrator.emitEvent({
		v = 1,
		cmd = "stop",
		gen = BG2Narrator.dialogueGeneration,
		reason = reason or "",
		ts = nowMillis(),
	})
end

function BG2Narrator.emitCancelSpeak(reason)
	if BG2Narrator.pendingSpeakId == "" then
		return
	end

	BG2Narrator.emitEvent({
		v = 1,
		cmd = "cancelSpeak",
		id = BG2Narrator.pendingSpeakId,
		reason = reason or "",
		ts = nowMillis(),
	})
	BG2Narrator.pendingSpeakId = ""
end

local function extractDisplaySpeaker(text)
	if text == nil or text == "" then
		return nil
	end

	local names = {}
	for name in text:gmatch("%^0[xX][0-9a-fA-F]+%s*(.-)%^%-") do
		local trimmed = name:match("^%s*(.-)%s*$")
		if trimmed ~= nil and trimmed ~= "" then
			table.insert(names, trimmed)
		end
	end

	if #names >= 1 then
		return names[1]
	end

	return nil
end

local function resolveNpcSpeaker(strRef, text, fallbackSpeaker)
	local displaySpeaker = extractDisplaySpeaker(text)
	if displaySpeaker ~= nil and displaySpeaker ~= "" then
		return displaySpeaker
	end

	if BG2Narrator.lastDialogTarget ~= nil and BG2Narrator.lastDialogTarget ~= "" then
		return BG2Narrator.lastDialogTarget
	end

	return fallbackSpeaker or ""
end

local function getUiVariant(variantField)
	if variantField == nil then
		return nil
	end

	if variantField.reference ~= nil then
		return variantField.reference
	end

	return variantField
end

local function resolveUiVariantRaw(variant)
	if variant == nil then
		return nil, nil
	end

	local ok, value = pcall(function()
		if variant.getValue then
			return variant:getValue()
		end
		return nil
	end)

	if not ok or value == nil then
		return nil, nil
	end

	if type(value) == "number" then
		local text = nil
		if Infinity_FetchString then
			local fetchOk, fetched = pcall(Infinity_FetchString, value)
			if fetchOk and fetched ~= nil and fetched ~= "" then
				text = fetched
			end
		end
		return value, text
	end

	if type(value) == "string" then
		return nil, value
	end

	if type(value) == "function" then
		local funcOk, fetched = pcall(value)
		if funcOk and fetched ~= nil and fetched ~= "" then
			return nil, tostring(fetched)
		end
	end

	return nil, nil
end

local function resolveUiTextBlock(textBlock)
	if textBlock == nil then
		return nil, nil
	end

	local originalText = safeString(textBlock.originalText)
	if originalText ~= "" then
		return nil, originalText
	end

	if textBlock.text ~= nil then
		return resolveUiVariantRaw(getUiVariant(textBlock.text))
	end

	return nil, nil
end

local function getItemName(item)
	if item == nil then
		return ""
	end

	return safeString(item.name)
end

local function getItemDialogue(item)
	if item == nil then
		return nil, nil
	end

	local strRef, text = resolveUiTextBlock(item.text)
	if text ~= nil and text ~= "" then
		return strRef, text
	end

	return resolveUiTextBlock(item.tooltip)
end

local function describeVariantField(value, fieldName)
	local ok, result = pcall(function()
		local field = value[fieldName]
		local parts = { fieldName .. "=" .. tostring(field) }
		if field == nil then
			return table.concat(parts, " ")
		end

		if field.text ~= nil then
			local variant = getUiVariant(field.text)
			if variant ~= nil then
				table.insert(parts, "variantType=" .. tostring(variant.type))
				if variant.value ~= nil then
					table.insert(parts, "int=" .. tostring(variant.value.intVal))
					table.insert(parts, "str=" .. safeString(variant.value.strVal))
				end
			end
		end

		return table.concat(parts, " ")
	end)

	return ok and result or (fieldName .. "=<debug-error>")
end

local function isUiProbeActive()
	return BG2Narrator.debugUiProbe and nowSeconds() < BG2Narrator.uiProbeUntil
end

local function logUiProbe(kind, value)
	if not isUiProbeActive() or value == nil then
		return
	end

	local name = getItemName(value)
	if name == "" then
		return
	end

	local _, text = getItemDialogue(value)
	text = text or ""
	local key = kind .. ":" .. name .. ":" .. text
	if BG2Narrator.uiProbeSeen[key] then
		return
	end

	BG2Narrator.uiProbeSeen[key] = true
	BG2Narrator.log(string.format("ui-probe %s name=%s text=%s", kind, name, text))

	local debugKey = kind .. ":" .. name .. ":debug"
	if not BG2Narrator.uiProbeDebugSeen[debugKey] then
		BG2Narrator.uiProbeDebugSeen[debugKey] = true
		BG2Narrator.log(string.format(
			"ui-debug %s name=%s %s | %s",
			kind,
			name,
			describeVariantField(value, "text"),
			describeVariantField(value, "tooltip")
		))
	end
end

local function startUiProbe(reason)
	if not BG2Narrator.debugUiProbe then
		return
	end

	BG2Narrator.uiProbeSeen = {}
	BG2Narrator.uiProbeDebugSeen = {}
	BG2Narrator.uiProbeUntil = nowSeconds() + BG2Narrator.uiProbeSeconds
	BG2Narrator.log("ui-probe-start reason=" .. reason)
end

function BG2Narrator.reportDialogueChunk(kind, strRef, text, speaker)
	if text == nil or text == "" then
		return
	end

	local resolvedSpeaker = resolveNpcSpeaker(strRef, text, speaker or BG2Narrator.lastDialogSpeaker)
	BG2Narrator.lastDialogSpeaker = resolvedSpeaker
	local key = kind .. "|" .. resolvedSpeaker .. "|" .. tostring(strRef or "") .. "|" .. text
	if BG2Narrator.seenDialogueChunks[key] then
		return
	end

	BG2Narrator.seenDialogueChunks[key] = true

	local strRefText = strRef and tostring(strRef) or ""
	BG2Narrator.log(string.format(
		"dialogue-%s speaker=%s strRef=%s text=%s",
		kind,
		resolvedSpeaker,
		strRefText,
		text
	))

	if kind ~= "npc" then
		return
	end

	BG2Narrator.dialogueGeneration = BG2Narrator.dialogueGeneration + 1
	BG2Narrator.emitStop("advance")
	BG2Narrator.pendingSpeakId = string.format(
		"%s-%s-%s",
		resolvedSpeaker,
		tostring(strRef or "none"),
		tostring(BG2Narrator.dialogueGeneration)
	)

	BG2Narrator.emitEvent({
		v = 1,
		cmd = "speak",
		id = BG2Narrator.pendingSpeakId,
		gen = BG2Narrator.dialogueGeneration,
		speaker = resolvedSpeaker,
		strRef = strRef or -1,
		rawText = text,
		ts = nowMillis(),
	})

	BG2Narrator.voObserverUntil = nowSeconds() + 0.5
end

local function restoreEeexMenuHooks()
	if EEex_Menu_Hook_OnBeforeUIItemRender == nil then
		return
	end

	EEex_Menu_Hook_OnBeforeUIItemRender = function(item)
		local listener = EEex_Menu_BeforeUIItemRenderListeners[item.name:get()]
		if listener then
			listener(item)
		end
	end

	if EEex_Menu_Hook_BeforeListRenderingItem == nil then
		return
	end

	EEex_Menu_Hook_BeforeListRenderingItem = function(list, item, window, rClipBase, alpha, menu)
		local listName = list.name:get()
		if listName ~= "" then
			local listeners = EEex_Menu_BeforeListRendersItemListeners[listName]
			if listeners then
				for _, listener in ipairs(listeners) do
					listener(list, item, window, rClipBase, alpha, menu)
				end
			end
		end
	end
end

local function onNpcDialogRender(item)
	logUiProbe("item", item)

	local strRef, text = getItemDialogue(item)
	if text == nil or text == "" then
		return
	end

	BG2Narrator.reportDialogueChunk("npc", strRef, text)
end

local function onPlayerChoiceRender(_list, item)
	logUiProbe("list-item", item)

	if not BG2Narrator.debugChoices then
		return
	end

	local strRef, text = getItemDialogue(item)
	BG2Narrator.reportDialogueChunk("choice", strRef, text)
end

local function logStartedAction(sprite, action)
	if action == nil then
		return
	end

	local actionId = action.m_actionID

	if actionId == BG2Narrator.playSoundThroughVoiceActionId then
		if nowSeconds() <= BG2Narrator.voObserverUntil then
			BG2Narrator.emitCancelSpeak("PlaySoundThroughVoice")
			BG2Narrator.log("vo-observer cancel pending TTS due to PlaySoundThroughVoice")
		end
		return
	end

	if not BG2Narrator.logAllActions and not BG2Narrator.dialogActionIds[actionId] then
		return
	end

	local spriteName = ""
	pcall(function()
		spriteName = safeString(sprite:getName())
	end)

	local targetName = ""
	if action.m_acteeID ~= nil then
		pcall(function()
			local target = EEex_GameObject_Get(action.m_acteeID.m_Instance)
			if target ~= nil and target.getName ~= nil then
				targetName = safeString(target:getName())
			end
		end)
	end

	BG2Narrator.lastDialogTarget = targetName
	BG2Narrator.seenDialogueChunks = {}

	BG2Narrator.log(string.format(
		"action-start id=%s sprite=%s target=%s string1=%s string2=%s source=%s targetInstance=%s",
		tostring(actionId),
		spriteName,
		targetName,
		safeString(action.m_string1),
		safeString(action.m_string2),
		safeString(action.m_source),
		action.m_acteeID and tostring(action.m_acteeID.m_Instance) or ""
	))

	startUiProbe("action-" .. tostring(actionId))
end

EEex_GameState_AddInitializedListener(function()
	restoreEeexMenuHooks()
	BG2Narrator.log("loaded version " .. BG2Narrator.version)

	if canUseIo() then
		pcall(function()
			local file = io.open(BG2Narrator.eventsPath, "w")
			if file then
				file:close()
			end
		end)
		BG2Narrator.log("ipc backend: events.jsonl")
	else
		BG2Narrator.log("ipc backend: Baldur.lua (EEex v1.0.0 has no Lua io library)")
	end

	if EEex_Action_AddSpriteStartedActionListener then
		EEex_Action_AddSpriteStartedActionListener(logStartedAction)
		BG2Narrator.log("registered action listener")
	else
		BG2Narrator.log("EEex action listener API not available")
	end

	if EEex_Menu_AddBeforeUIItemRenderListener then
		EEex_Menu_AddBeforeUIItemRenderListener("worldNPCDialog", onNpcDialogRender)
		BG2Narrator.log("registered worldNPCDialog listener")
	else
		BG2Narrator.log("EEex UI item listener API not available")
	end

	if EEex_Menu_AddBeforeListRendersItemListener then
		EEex_Menu_AddBeforeListRendersItemListener("worldPlayerDialogChoicesList", onPlayerChoiceRender)
		BG2Narrator.log("registered worldPlayerDialogChoicesList listener")
	else
		BG2Narrator.log("EEex list item listener API not available")
	end
end)


