// RealNestStories - self-contained module. Does not touch any existing JS.
(function () {
    'use strict';

    var IMAGE_DURATION_MS = 5000;
    var SWIPE_THRESHOLD_PX = 50;

    var groups = [];
    var groupIndex = 0;
    var storyIndex = 0;
    var mode = 'feed'; // 'feed' (customer viewing followed stores) or 'own' (store owner previewing their own)

    var timerHandle = null;   // image-only auto-advance timer
    var progressRAF = null;   // image-only progress animation
    var storyStartTime = 0;   // image-only elapsed tracking
    var pausedElapsedMs = null;
    var isOpen = false;

    function byId(id) { return document.getElementById(id); }

    // ================= Modal open/close - this project's own convention =================
    function showBackdrop(id) {
        var el = byId(id);
        if (el) el.classList.add('open');
    }
    function hideBackdrop(id) {
        var el = byId(id);
        if (el) el.classList.remove('open');
    }

    function openUploadStoryModal() { showBackdrop('uploadStoryBackdrop'); }
    function closeUploadStoryModal() { hideBackdrop('uploadStoryBackdrop'); resetUploadDialog(); }

    // ================= Helpers =================
    function currentStory() {
        var group = groups[groupIndex];
        return group ? group.stories[storyIndex] : null;
    }

    function isVideoStory(story) {
        return story && story.mediaType === 'Video';
    }

    function clearImageTimers() {
        if (timerHandle) { clearTimeout(timerHandle); timerHandle = null; }
        if (progressRAF) { cancelAnimationFrame(progressRAF); progressRAF = null; }
    }

    function buildProgressBars(count) {
        var row = byId('storyProgressRow');
        if (!row) return;
        row.innerHTML = '';
        for (var i = 0; i < count; i++) {
            var bar = document.createElement('div');
            bar.className = 'story-progress-bar';
            var fill = document.createElement('div');
            fill.className = 'story-progress-fill';
            bar.appendChild(fill);
            row.appendChild(bar);
        }
    }

    function setStaticProgress(upToIndex) {
        var fills = document.querySelectorAll('#storyProgressRow .story-progress-fill');
        fills.forEach(function (fill, i) {
            fill.style.width = i < upToIndex ? '100%' : '0%';
        });
    }

    function currentProgressFill() {
        var fills = document.querySelectorAll('#storyProgressRow .story-progress-fill');
        return fills[storyIndex];
    }

    // ---- Image progress: timer + requestAnimationFrame ----
    function animateImageFrom(elapsedMs) {
        var fill = currentProgressFill();
        if (!fill) return;
        storyStartTime = performance.now() - elapsedMs;

        function step(now) {
            var elapsed = now - storyStartTime;
            var pct = Math.min(100, (elapsed / IMAGE_DURATION_MS) * 100);
            fill.style.width = pct + '%';
            if (pct < 100) progressRAF = requestAnimationFrame(step);
        }
        progressRAF = requestAnimationFrame(step);

        var remaining = Math.max(0, IMAGE_DURATION_MS - elapsedMs);
        timerHandle = setTimeout(next, remaining);
    }

    // ---- Video progress: driven by the <video> element's own timeupdate/ended events ----
    function getVideoEl() { return byId('storyViewerVideo'); }

    function onVideoTimeUpdate() {
        var video = getVideoEl();
        var fill = currentProgressFill();
        if (!video || !fill || !video.duration) return;
        var pct = Math.min(100, (video.currentTime / video.duration) * 100);
        fill.style.width = pct + '%';
    }

    function onVideoEnded() {
        next();
    }

    // ================= Pause / resume (hold to pause, like Instagram) =================
    function pause() {
        if (pausedElapsedMs !== null) return;
        var story = currentStory();
        if (isVideoStory(story)) {
            var video = getVideoEl();
            if (video) video.pause();
            pausedElapsedMs = 0; // marker only - video keeps its own currentTime
        } else {
            pausedElapsedMs = performance.now() - storyStartTime;
            clearImageTimers();
        }
    }

    function resume() {
        if (pausedElapsedMs === null) return;
        var story = currentStory();
        if (isVideoStory(story)) {
            var video = getVideoEl();
            if (video) video.play().catch(function () { /* autoplay restrictions - user gesture already occurred via hold */ });
            pausedElapsedMs = null;
        } else {
            var elapsed = pausedElapsedMs;
            pausedElapsedMs = null;
            animateImageFrom(elapsed);
        }
    }

    // ================= Rendering =================
    function formatRelativeTime(isoString) {
        var then = new Date(isoString);
        var diffMin = Math.floor((Date.now() - then.getTime()) / 60000);
        if (diffMin < 1) return 'Just now';
        if (diffMin < 60) return diffMin + 'm ago';
        return Math.floor(diffMin / 60) + 'h ago';
    }

    function render() {
        clearImageTimers();
        pausedElapsedMs = null;

        var group = groups[groupIndex];
        if (!group) { close(); return; }
        var story = group.stories[storyIndex];
        if (!story) { close(); return; }

        buildProgressBars(group.stories.length);
        setStaticProgress(storyIndex);

        byId('storyViewerAvatar').src = group.storeLogoUrl || '/images/store.png';
        byId('storyViewerStoreName').textContent = group.storeName || '';
        byId('storyViewerTime').textContent = formatRelativeTime(story.createdAt);
        byId('storyViewerCaption').textContent = story.caption || '';

        var imgEl = byId('storyViewerImage');
        var videoEl = getVideoEl();

        if (isVideoStory(story)) {
            imgEl.style.display = 'none';
            videoEl.style.display = 'block';
            videoEl.classList.remove('story-media--loaded');
            videoEl.src = story.videoUrl;
            videoEl.currentTime = 0;
            videoEl.onloadeddata = function () { videoEl.classList.add('story-media--loaded'); };
            videoEl.play().catch(function () { /* ignore autoplay rejection - user already interacted to open the viewer */ });
            // timeupdate/ended listeners are attached once at init (see wireVideoElement), reading module state at fire time
        } else {
            videoEl.pause();
            videoEl.style.display = 'none';
            imgEl.style.display = 'block';
            imgEl.classList.remove('story-media--loaded');
            imgEl.onload = function () { imgEl.classList.add('story-media--loaded'); };
            imgEl.src = story.imageUrl;
            animateImageFrom(0);
        }

        var deleteBtn = byId('storyViewerDeleteBtn');
        if (deleteBtn) deleteBtn.style.display = mode === 'own' ? 'inline-flex' : 'none';

        var actionBar = byId('storyActionBar');
        var insightsBar = byId('storyInsightsBar');

        if (mode === 'own') {
            if (actionBar) actionBar.style.display = 'none';
            if (insightsBar) {
                insightsBar.style.display = 'flex';
                byId('insightsViewCount').textContent = story.viewCount || 0;
                byId('insightsLikeCount').textContent = story.likeCount || 0;
                byId('insightsReplyCount').textContent = story.replyCount || 0;
            }
            closeInsightsPanel(); // reset panel closed on every story change
        } else {
            if (insightsBar) insightsBar.style.display = 'none';
            if (actionBar) {
                actionBar.style.display = 'flex';
                updateLikeButton(story.isLiked);
            }
            var replyInput = byId('storyReplyInput');
            var replyError = byId('storyReplyError');
            if (replyInput) replyInput.value = '';
            if (replyError) replyError.style.display = 'none';
        }

        if (mode === 'feed' && !story.isViewed) {
            markCurrentStoryViewed(story, group);
        }
    }

    function next() {
        var group = groups[groupIndex];
        if (!group) { close(); return; }

        if (storyIndex < group.stories.length - 1) {
            storyIndex += 1;
            render();
        } else if (groupIndex < groups.length - 1) {
            groupIndex += 1;
            storyIndex = 0;
            render();
        } else {
            close();
        }
    }

    function prev() {
        if (storyIndex > 0) {
            storyIndex -= 1;
            render();
        } else if (groupIndex > 0) {
            groupIndex -= 1;
            storyIndex = groups[groupIndex].stories.length - 1;
            render();
        } else {
            render(); // already at the very first story - restart it
        }
    }

    function close() {
        clearImageTimers();
        pausedElapsedMs = null;
        var video = getVideoEl();
        if (video) video.pause();
        hideBackdrop('storyViewerBackdrop');
        isOpen = false;
    }

    // ================= Entry points =================
    function open(gIndex, sIndex) {
        mode = 'feed';
        groups = window.realnestStoryGroups || [];
        groupIndex = gIndex;
        storyIndex = sIndex;
        isOpen = true;
        showBackdrop('storyViewerBackdrop');
        render();
    }

    function openOwn(sIndex) {
        mode = 'own';
        groups = [{
            storeName: '',
            storeLogoUrl: null,
            stories: window.realnestOwnStories || []
        }];
        groupIndex = 0;
        storyIndex = sIndex;
        isOpen = true;
        showBackdrop('storyViewerBackdrop');
        render();
    }

    // ================= Viewed state (DB-backed) =================
    function getAntiForgeryToken() {
        var el = document.querySelector('#storyAntiForgeryForm input[name="__RequestVerificationToken"]');
        return el ? el.value : null;
    }

    function markCurrentStoryViewed(story, group) {
        story.isViewed = true;

        var stillUnviewed = group.stories.some(function (s) { return !s.isViewed; });
        group.hasUnviewedStories = stillUnviewed;

        // Works for both the Feed's circle bar (.story-circle > .story-ring) and
        // StoreCustomerProfile's single avatar (.avatar-ring itself carries the classes) -
        // same shared function, just two possible ring locations depending on the page.
        var circle = document.querySelector('.story-circle[data-group-index="' + groupIndex + '"] .story-ring') ||
                     document.querySelector('.avatar-ring[data-group-index="' + groupIndex + '"]');
        if (circle) {
            circle.classList.toggle('story-ring--active', stillUnviewed);
            circle.classList.toggle('story-ring--viewed', !stillUnviewed);
        }

        var token = getAntiForgeryToken();
        var body = new URLSearchParams();
        body.append('storyId', story.storyId);
        if (token) body.append('__RequestVerificationToken', token);

        fetch(window.location.pathname + window.location.search +
            (window.location.search ? '&' : '?') + 'handler=MarkStoryViewed', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: body.toString()
        })
            .then(function (response) {
                if (!response.ok) {
                    console.error('RealNestStories: mark-viewed request failed with status', response.status, '- the view was NOT saved to the database.');
                }
                return response.json().catch(function () { return null; });
            })
            .then(function (data) {
                if (data && data.success === false) {
                    console.error('RealNestStories: server rejected mark-viewed for storyId', story.storyId, '- the view was NOT saved to the database.');
                }
            })
            .catch(function (err) {
                console.error('RealNestStories: mark-viewed network error - the view was NOT saved to the database.', err);
            });
    }

    // ================= Delete (Store Owner only, own stories) =================
    function deleteCurrent() {
        if (mode !== 'own') return;
        var story = currentStory();
        if (!story) return;
        if (!window.confirm('Delete this story? This cannot be undone.')) return;

        var idInput = byId('deleteStoryIdInput');
        var form = byId('deleteStoryForm');
        if (idInput && form) {
            idInput.value = story.storyId;
            form.submit();
        }
    }

    // ================= LIKE (customer only) =================
    function updateLikeButton(isLiked) {
        var icon = byId('storyLikeIcon');
        if (!icon) return;
        icon.classList.toggle('fa-regular', !isLiked);
        icon.classList.toggle('fa-solid', isLiked);
        icon.classList.toggle('story-liked', isLiked);
    }

    function toggleLike() {
        if (mode !== 'feed') return;
        var story = currentStory();
        if (!story) return;

        var token = getAntiForgeryToken();
        var body = new URLSearchParams();
        body.append('storyId', story.storyId);
        if (token) body.append('__RequestVerificationToken', token);

        fetch(window.location.pathname + window.location.search +
            (window.location.search ? '&' : '?') + 'handler=ToggleStoryLike', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: body.toString()
        })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (data && data.success) {
                    story.isLiked = data.liked;
                    updateLikeButton(story.isLiked);
                } else {
                    console.error('RealNestStories: like toggle failed', data);
                }
            })
            .catch(function (err) { console.error('RealNestStories: like toggle network error', err); });
    }

    // ================= REPLY (customer only) - reuses the existing chat system =================
    function submitReply(event) {
        if (event) event.preventDefault();
        if (mode !== 'feed') return false;

        var story = currentStory();
        var input = byId('storyReplyInput');
        var sendBtn = byId('storyReplySendBtn');
        var errorEl = byId('storyReplyError');
        if (!story || !input) return false;

        var text = input.value.trim();
        errorEl.style.display = 'none';

        if (!text) {
            errorEl.textContent = 'Please write a reply before sending.';
            errorEl.style.display = 'block';
            return false;
        }

        sendBtn.disabled = true;
        var originalLabel = sendBtn.textContent;
        sendBtn.textContent = 'Sending...';

        var token = getAntiForgeryToken();
        var body = new URLSearchParams();
        body.append('storyId', story.storyId);
        body.append('replyText', text);
        if (token) body.append('__RequestVerificationToken', token);

        fetch(window.location.pathname + window.location.search +
            (window.location.search ? '&' : '?') + 'handler=ReplyToStory', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: body.toString()
        })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (data && data.success) {
                    input.value = '';
                    errorEl.style.display = 'none';
                } else {
                    errorEl.textContent = (data && data.error) || 'Could not send your reply. Please try again.';
                    errorEl.style.display = 'block';
                }
            })
            .catch(function () {
                errorEl.textContent = 'Network error - could not send your reply.';
                errorEl.style.display = 'block';
            })
            .finally(function () {
                sendBtn.disabled = false;
                sendBtn.textContent = originalLabel;
            });

        return false;
    }

    // ================= STORY INSIGHTS (Store Owner only) =================
    function renderPersonRow(person, actionLabel) {
        var initial = (person.fullName || 'C').trim().charAt(0).toUpperCase();
        var when = formatRelativeTime(person.actionAt || person.sentAt);
        var usernameHtml = person.userName ? '<span class="story-insight-username">@' + escapeHtml(person.userName) + '</span>' : '';
        var replyHtml = person.replyText ? '<div class="story-insight-reply-text">' + escapeHtml(person.replyText) + '</div>' : '';
        return '' +
            '<div class="story-insight-row">' +
            '  <div class="story-insight-avatar">' + initial + '</div>' +
            '  <div class="story-insight-info">' +
            '    <div class="story-insight-name">' + escapeHtml(person.fullName || 'Customer') + ' ' + usernameHtml + '</div>' +
            replyHtml +
            '    <div class="story-insight-time">' + actionLabel + ' ' + when + '</div>' +
            '  </div>' +
            '</div>';
    }

    function escapeHtml(str) {
        var div = document.createElement('div');
        div.textContent = str == null ? '' : String(str);
        return div.innerHTML;
    }

    function renderInsightsList(containerId, items, actionLabel, emptyText) {
        var container = byId(containerId);
        if (!container) return;
        if (!items || items.length === 0) {
            container.innerHTML = '<div class="story-insight-empty">' + escapeHtml(emptyText) + '</div>';
            return;
        }
        container.innerHTML = items.map(function (item) { return renderPersonRow(item, actionLabel); }).join('');
    }

    function openInsightsPanel() {
        if (mode !== 'own') return;
        var story = currentStory();
        if (!story || !window.realnestInsightsUrl) return;

        var panel = byId('storyInsightsPanel');
        if (panel) panel.classList.add('open');

        fetch(window.realnestInsightsUrl + '?handler=StoryInsights&storyId=' + story.storyId)
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (!data || !data.success || !data.insights) return;
                var insights = data.insights;

                byId('insightsSummaryViews').textContent = insights.totalViews;
                byId('insightsSummaryUnique').textContent = insights.totalViews; // always equal - one row per customer
                byId('insightsSummaryLikes').textContent = insights.totalLikes;
                byId('insightsSummaryReplies').textContent = insights.totalReplies;

                renderInsightsList('storyInsightsViewersList', insights.viewers, 'Viewed', 'No one has viewed this story yet.');
                renderInsightsList('storyInsightsLikesList', insights.likes, 'Liked', 'No likes yet.');
                renderInsightsList('storyInsightsRepliesList', insights.replies, 'Replied', 'No replies yet.');
            })
            .catch(function (err) { console.error('RealNestStories: insights fetch failed', err); });
    }

    function closeInsightsPanel() {
        var panel = byId('storyInsightsPanel');
        if (panel) panel.classList.remove('open');
    }

    function switchInsightsTab(tab) {
        document.querySelectorAll('.story-insights-tab').forEach(function (btn) {
            btn.classList.toggle('active', btn.dataset.tab === tab);
        });
        byId('storyInsightsViewersList').style.display = tab === 'viewers' ? 'block' : 'none';
        byId('storyInsightsLikesList').style.display = tab === 'likes' ? 'block' : 'none';
        byId('storyInsightsRepliesList').style.display = tab === 'replies' ? 'block' : 'none';
    }

    // ================= Keyboard / click-zone / swipe navigation =================
    document.addEventListener('keydown', function (e) {
        if (!isOpen) return;
        if (e.key === 'ArrowRight') { e.preventDefault(); next(); }
        else if (e.key === 'ArrowLeft') { e.preventDefault(); prev(); }
        else if (e.key === 'Escape') { e.preventDefault(); close(); }
    });

    document.addEventListener('DOMContentLoaded', function () {
        var backdrop = byId('storyViewerBackdrop');
        if (backdrop) {
            backdrop.addEventListener('click', function (e) {
                if (e.target === backdrop) close(); // click outside the content closes it
            });
        }

        var videoEl = getVideoEl();
        if (videoEl) {
            videoEl.addEventListener('timeupdate', onVideoTimeUpdate);
            videoEl.addEventListener('ended', onVideoEnded);
        }

        var stage = byId('storyViewerStage');
        if (!stage) return;

        var leftZone = stage.querySelector('.story-tap-zone--left');
        var rightZone = stage.querySelector('.story-tap-zone--right');
        if (leftZone) leftZone.addEventListener('click', function () { prev(); });
        if (rightZone) rightZone.addEventListener('click', function () { next(); });

        stage.addEventListener('mousedown', pause);
        stage.addEventListener('mouseup', resume);
        stage.addEventListener('mouseleave', function () { if (pausedElapsedMs !== null) resume(); });

        var touchStartX = 0;
        stage.addEventListener('touchstart', function (e) {
            touchStartX = e.changedTouches[0].screenX;
            pause();
        }, { passive: true });

        stage.addEventListener('touchend', function (e) {
            var diff = e.changedTouches[0].screenX - touchStartX;
            resume();
            if (Math.abs(diff) >= SWIPE_THRESHOLD_PX) {
                if (diff < 0) next(); else prev();
            }
        }, { passive: true });
    });

    // ================= Upload modal v2 (dropzone + live preview + validation) =================
    var ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
    var ALLOWED_VIDEO_TYPES = ['video/mp4', 'video/webm', 'video/quicktime'];
    var MAX_UPLOAD_BYTES = 25 * 1024 * 1024; // 25 MB
    var currentPreviewObjectUrl = null;

    function showUploadError(message) {
        var box = byId('storyUploadErrorV2');
        var text = byId('storyUploadErrorTextV2');
        if (!box || !text) return;
        text.textContent = message;
        box.style.display = 'flex';
    }

    function hideUploadError() {
        var box = byId('storyUploadErrorV2');
        if (box) box.style.display = 'none';
    }

    function setShareButtonEnabled(enabled) {
        var btn = byId('storyShareBtnV2');
        if (btn) btn.disabled = !enabled;
    }

    function resetUploadDialog() {
        var input = byId('storyMediaInput');
        var placeholder = byId('storyDropzonePlaceholder');
        var preview = byId('storyDropzonePreview');
        var caption = byId('storyCaptionInputV2');
        var counter = byId('storyCaptionCount');
        var durationField = byId('storyDurationSecondsInput');
        var dropzone = byId('storyDropzone');

        if (input) input.value = '';
        if (placeholder) placeholder.style.display = 'flex';
        if (preview) { preview.style.display = 'none'; preview.innerHTML = ''; }
        if (caption) caption.value = '';
        if (counter) counter.textContent = '0';
        if (durationField) durationField.value = '';
        if (dropzone) dropzone.classList.remove('story-dropzone--dragging');

        if (currentPreviewObjectUrl) { URL.revokeObjectURL(currentPreviewObjectUrl); currentPreviewObjectUrl = null; }

        hideUploadError();
        setShareButtonEnabled(false);

        var shareBtn = byId('storyShareBtnV2');
        var spinner = byId('storyShareBtnSpinner');
        var icon = byId('storyShareBtnIcon');
        var label = byId('storyShareBtnLabel');
        if (shareBtn) shareBtn.classList.remove('story-share-btn--loading');
        if (spinner) spinner.style.display = 'none';
        if (icon) icon.style.display = 'inline-block';
        if (label) label.textContent = 'Share Story';
    }

    function validateStoryFile(file) {
        var isImage = ALLOWED_IMAGE_TYPES.indexOf(file.type) !== -1;
        var isVideo = ALLOWED_VIDEO_TYPES.indexOf(file.type) !== -1;

        if (!file.type || (!isImage && !isVideo)) {
            return 'Unsupported image format. Please use JPG, PNG, or WEBP (MP4/WEBM for video).';
        }
        if (file.size > MAX_UPLOAD_BYTES) {
            return 'Image exceeds maximum size of 25MB.';
        }
        return null;
    }

    function onStoryFileSelected(file) {
        hideUploadError();

        if (!file) {
            showUploadError('Please select an image.');
            setShareButtonEnabled(false);
            return;
        }

        var error = validateStoryFile(file);
        if (error) {
            showUploadError(error);
            setShareButtonEnabled(false);
            var input = byId('storyMediaInput');
            if (input) input.value = '';
            return;
        }

        // Sync the actual <input type="file"> with the chosen file, so drag & drop
        // (which doesn't go through the input's own change event) still submits correctly.
        var fileInput = byId('storyMediaInput');
        if (fileInput) {
            try {
                var dt = new DataTransfer();
                dt.items.add(file);
                fileInput.files = dt.files;
            } catch (e) { /* DataTransfer construction unsupported - input already has the file if selected via click */ }
        }

        var isVideo = ALLOWED_VIDEO_TYPES.indexOf(file.type) !== -1;
        var durationField = byId('storyDurationSecondsInput');
        if (durationField) durationField.value = '';

        if (currentPreviewObjectUrl) URL.revokeObjectURL(currentPreviewObjectUrl);
        currentPreviewObjectUrl = URL.createObjectURL(file);

        var placeholder = byId('storyDropzonePlaceholder');
        var preview = byId('storyDropzonePreview');

        if (placeholder) placeholder.style.display = 'none';
        if (preview) { preview.style.display = 'block'; preview.innerHTML = ''; }

        var dropEl = document.createElement(isVideo ? 'video' : 'img');
        dropEl.src = currentPreviewObjectUrl;
        dropEl.className = 'story-dropzone__preview-media';
        if (isVideo) { dropEl.muted = true; dropEl.loop = true; dropEl.autoplay = true; dropEl.playsInline = true; }
        if (preview) preview.appendChild(dropEl);

        if (isVideo) {
            dropEl.addEventListener('loadedmetadata', function () {
                if (durationField) durationField.value = Math.round(dropEl.duration);
            });
        }

        setShareButtonEnabled(true);
    }

    function onCaptionInput(value) {
        var counter = byId('storyCaptionCount');
        if (counter) counter.textContent = String(value.length);
    }

    function onUploadSubmit(event) {
        var btn = byId('storyShareBtnV2');
        if (!btn || btn.disabled) { if (event) event.preventDefault(); return false; }

        // Prevent multiple submissions
        btn.disabled = true;
        btn.classList.add('story-share-btn--loading');
        var spinner = byId('storyShareBtnSpinner');
        var icon = byId('storyShareBtnIcon');
        var label = byId('storyShareBtnLabel');
        if (spinner) spinner.style.display = 'inline-block';
        if (icon) icon.style.display = 'none';
        if (label) label.textContent = 'Uploading...';

        return true; // let the classic form POST proceed
    }

    document.addEventListener('DOMContentLoaded', function () {
        var dropzone = byId('storyDropzone');
        if (!dropzone) return;

        ['dragenter', 'dragover'].forEach(function (evt) {
            dropzone.addEventListener(evt, function (e) {
                e.preventDefault();
                e.stopPropagation();
                dropzone.classList.add('story-dropzone--dragging');
            });
        });

        ['dragleave', 'drop'].forEach(function (evt) {
            dropzone.addEventListener(evt, function (e) {
                e.preventDefault();
                e.stopPropagation();
                dropzone.classList.remove('story-dropzone--dragging');
            });
        });

        dropzone.addEventListener('drop', function (e) {
            var file = e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0];
            if (file) onStoryFileSelected(file);
        });
    });

    window.RealNestStories = {
        open: open,
        openOwn: openOwn,
        next: next,
        prev: prev,
        deleteCurrent: deleteCurrent,
        openUploadStoryModal: openUploadStoryModal,
        closeUploadStoryModal: closeUploadStoryModal,
        onStoryFileSelected: onStoryFileSelected,
        onCaptionInput: onCaptionInput,
        onUploadSubmit: onUploadSubmit,
        closeViewer: close,
        toggleLike: toggleLike,
        submitReply: submitReply,
        openInsightsPanel: openInsightsPanel,
        closeInsightsPanel: closeInsightsPanel,
        switchInsightsTab: switchInsightsTab
    };
})();
