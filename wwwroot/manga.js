function setBookmark() {
    const page = window.location.hash.slice(1);
    const { manga, chapter } = document.body.dataset
    const bookmark = page ? `${chapter}/${page}` : chapter;
    fetch(`/manga/${manga}/bookmark`, { method: "PUT", body: bookmark });
}

function setLastManga() {
    const { manga } = document.body.dataset;
    fetch("/manga/last-manga", { method: "PUT", body: manga });
}

function hashChangeHandler() {
    const page = window.location.hash.slice(1);

    const imgs = Array.from(document.images);
    imgs.forEach(i => i.classList.remove("active"));
    imgs.find(i => i.dataset.page === page).classList.add("active");

    const options = Array.from(document.querySelectorAll("#page-select option"));
    options.forEach(o => o.selected = false);
    options.find(o => o.value === page).selected = true;

    setBookmark();
}

function chapterSelectHandler(e) {
    window.location.href = e.target.value;
}

function pageSelectHandler(e) {
    window.location.hash = e.target.value;
}

function previousHandler() {
    const currentImage = document.querySelector(".active");
    const previousImage = currentImage ? currentImage.previousElementSibling : null;
    if (document.body.dataset.direction === "horizontal" && previousImage) {
        location.hash = previousImage.dataset.page;
    } else if (document.body.dataset.previousPage) {
        window.location.href = document.body.dataset.previousPage;
    }
}

function nextHandler() {
    const currentImage = document.querySelector(".active");
    const nextImage = currentImage ? currentImage.nextElementSibling : null;
    if (document.body.dataset.direction === "horizontal" && nextImage) {
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

function init() {
    if (document.body.dataset.direction === "horizontal") {
        window.addEventListener("hashchange", hashChangeHandler);

        if (window.location.hash) {
            hashChangeHandler();
        } else {
            window.location.hash = document.images[0].dataset.page;
        }

        const pageSelect = document.getElementById("page-select");
        pageSelect.addEventListener("change", pageSelectHandler);
    } else {
        setBookmark();
    }

    document.getElementById("chapter-select").addEventListener("change", chapterSelectHandler);
    document.addEventListener("keydown", keyHandler);

    setLastManga();
}

document.addEventListener("DOMContentLoaded", init);
