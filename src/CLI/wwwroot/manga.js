async function setBookmark() {
    const page = window.location.hash.slice(1);
    const pageId = document.querySelector(`img[data-page="${page}"]`)?.dataset?.pageId;
    const { mangaId, chapterId } = document.body.dataset;
    await fetch(`/api/manga/${mangaId}/bookmark`, {
        method: "PUT",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            ChapterId: chapterId,
            PageId: document.body.dataset.direction === "Horizontal" ? pageId : null
        })
    });
}

async function hashChangeHandler() {
    const page = window.location.hash.slice(1);
    if (page) {
        const pageId = document.querySelector(`img[data-page="${page}"]`).dataset.pageId;
    
        const imgs = Array.from(document.images);
        imgs.forEach(i => i.classList.remove("active"));
        imgs.find(i => i.dataset.pageId === pageId).classList.add("active");
    
        const options = Array.from(document.querySelectorAll("#page-select option"));
        options.forEach(o => o.selected = false);
        options.find(o => o.value === pageId).selected = true;
    
    }
    await setBookmark();
}

function chapterSelectHandler(e) {
    window.location.href = e.target.value;
}

function pageSelectHandler(e) {
    const pageId = e.target.value;
    const page = document.querySelector(`img[data-page-id="${pageId}"]`).dataset.page;
    window.location.hash = page;
}

function previousHandler() {
    const currentImage = document.querySelector(".active");
    const previousImage = currentImage ? currentImage.previousElementSibling : null;
    if (document.body.dataset.direction === "Horizontal" && previousImage) {
        location.hash = previousImage.dataset.page;
    } else if (document.body.dataset.previousPage) {
        window.location.href = document.body.dataset.previousPage;
    }
}

function nextHandler() {
    const currentImage = document.querySelector(".active");
    const nextImage = currentImage ? currentImage.nextElementSibling : null;
    if (document.body.dataset.direction === "Horizontal" && nextImage) {
        location.hash = nextImage.dataset.page;
    } else if (document.body.dataset.nextPage) {
        window.location.href = document.body.dataset.nextPage;
    }
}

function keyHandler(e) {
    switch (e.key) {
        case "ArrowLeft":
        case "ArrowUp":
        case "Backspace":
        case "k":
            previousHandler();
            e.preventDefault();
            break;
        case "ArrowRight":
        case "ArrowDown":
        case " ":
        case "j":
            nextHandler();
            e.preventDefault();
            break;
        case "<":
            const previousPage = document.body.dataset.previousPage;
            if (previousPage) {
                window.location.href = previousPage.split("#")[0];
            }
            e.preventDefault();
            break;
        case ">":
            const nextPage = document.body.dataset.nextPage;
            if (nextPage) {
                window.location.href = nextPage.split("#")[0];
            }
            e.preventDefault();
            break;
        default:
            break;
    }
}

async function init() {
    if (document.body.dataset.direction === "Horizontal") {
        window.addEventListener("hashchange", hashChangeHandler);

        if (window.location.hash) {
            await hashChangeHandler();
        } else {
            const hash = document.images[0]?.dataset?.page || "";
            if (hash) {
                window.location.hash = hash;
            } else {
                await setBookmark();
            }
        }

        const pageSelect = document.getElementById("page-select");
        if (pageSelect) {
            pageSelect.addEventListener("change", pageSelectHandler);
        }
    } else {
        await setBookmark();
    }

    document.getElementById("chapter-select").addEventListener("change", chapterSelectHandler);
    document.addEventListener("keydown", keyHandler);
}

document.addEventListener("DOMContentLoaded", init);
