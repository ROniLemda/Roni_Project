// כתובת השרת
const API_URL = 'http://localhost:5057';

// בעת טעינת הדף
window.onload = function()
{
    // חיבור כפתור ההתנתקות
    const logoutBtn = document.getElementById('adminLogoutBtn');
    if (logoutBtn) 
    {
        logoutBtn.onclick = handleLogout;
    }

    // חיבור שורת החיפוש
    const searchInput = document.getElementById('searchInput');
    searchInput.oninput = function()
    {
        searchUsers(); // פעולת החיפוש
    };

    // חיבור כפתור הרענון
    const refreshBtn = document.getElementById('refreshBtn');
    if (refreshBtn)
    {
        refreshBtn.onclick = function()
        {
            loadUsers(); // פעולת מילוי הטבלה
            loadLastAction(); // טעינת הפעולה האחרונה ממסד הנתונים
        };
    }

    // טעינת המשתמשים והפעולה האחרונה מהשרת
    loadUsers();
    loadLastAction();
};

// פונקציה לשליפת הפעולה האחרונה ממסד הנתונים
async function loadLastAction() 
{
    // שליפת הטוקן של המנהל מהזיכרון של הדפדפן
    const authToken = localStorage.getItem('authToken');

    // שליחת בקשה לשרת עם הטוקן
    const response = await fetch(API_URL + '/api/admin/logs?token=' + authToken);
    const logs = await response.json();

    // אם קיימות פעולות ביומן במסד הנתונים
    if (logs.length > 0) 
    {
        // הפעולה הראשונה היא הפעולה העדכנית ביותר שנשמרה
        const lastLog = logs[0];
        document.getElementById('lastActivityText').textContent = lastLog.action_type + ' (' + lastLog.created_at + ')';
    }
}

// פונקציית התנתקות
async function handleLogout() 
{
    const confirmResult = await Swal.fire({
        title: 'התנתקות מהמערכת',
        text: 'לצורך שמירה על אבטחת המידע, האם אתה בטוח שברצונך לצאת מחשבונך ולבצע התנתקות מהחשבון?',
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#0a4ade',
        cancelButtonColor: '#94a3b8',
        confirmButtonText: 'אישור ויציאה',
        cancelButtonText: 'ביטול'
    });

    if (confirmResult.isConfirmed === true) 
    {
        const authToken = localStorage.getItem('authToken');
        if (authToken) 
        {
            await fetch(API_URL + '/api/logout', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ token: authToken })
            });
        }
        localStorage.clear(); // מוחק את הלוקל סטורג 
        window.location.replace('../login.html'); // מעביר ישר ללוגין והנקודה בהתחלה אומר לו לצאת מהתקייה
    }
}

// פונקציה לשליפת המשתמשים מהשרת והצגתם בטבלה
async function loadUsers() 
{
    // שליפת הטוקן של המנהל מהזיכרון של הדפדפן
    const authToken = localStorage.getItem('authToken');

    // שולחים בקשה לשרת יחד עם הטוקן ומקבלים את הרשימה מהמסד
    const response = await fetch(API_URL + '/api/admin/users?token=' + authToken);
    const users = await response.json();

    //  משתנים לשורות ולספירת הסטטיסטיקות
    let rowsHtml = '';
    let totalCount = users.length;
    let verifiedCount = 0;
    let pendingCount = 0;

    //  לולאה שעוברת משתמש-משתמש
    for (let i = 0; i < users.length; i++) 
    {
        const u = users[i];

        // בדיקת אימות אם מאומת סופרים ומציגים תגית ירוקה, אחרת כתומה
        let statusBadge = '<span class="badge-pending">ממתין</span>';
        if (u.is_verified === true) 
        {
            statusBadge = '<span class="badge-verified">מאומת</span>';
            verifiedCount++;
        }
        else 
        {
            pendingCount++;
        }

            // בדיקה האם יש ערכים או שהם ריקים (במקום סימן שאלה ונקודתיים)
            let resetHash = '-';
            if (u.reset_code_hash !== null && u.reset_code_hash !== '') 
            {
                resetHash = u.reset_code_hash;
            }

            let resetExpires = '-';
            if (u.reset_code_expires !== null && u.reset_code_expires !== '') 
            {
                resetExpires = u.reset_code_expires;
            }

            let authToken = '-';
            if (u.auth_token !== null && u.auth_token !== '') 
            {
                authToken = u.auth_token;
            }

        // הרכבת שורת הטבלה
        // כעת גם אם משתמש יירשם עם שם שמכיל תגיות HTML או סקריפטים הדפדפן של המנהל יציג אותם כטקסט תמים ולא יריץ שום קוד זדוני
        rowsHtml += 
        '<tr>' + // פותח טבלה
            '<td><strong>#' + u.id + '</strong></td>' +
            '<td>' + safeText(u.full_name) + '</td>' +
            '<td>' + safeText(u.username) + '</td>' +
            '<td>' + safeText(u.email) + '</td>' +
            '<td><span class="hash-text">' + u.password_hash + '</span></td>' +
            '<td>' + statusBadge + '</td>' +
            '<td><span class="hash-text">' + resetHash + '</span></td>' +
            '<td>' + resetExpires + '</td>' +
            '<td><span class="hash-text">' + authToken + '</span></td>' +
            '<td>' +
                '<div class="action-btns">' +
                    '<button class="btn-table-action btn-edit" onclick=\'editUser(' + JSON.stringify(u) + ')\' title="ערוך משתמש">' +
                        '<i class="fa-solid fa-pen-to-square"></i>' +
                    '</button>' +
                    '<button class="btn-table-action btn-delete" onclick="deleteUser(' + u.id + ')" title="מחק משתמש">' +
                        '<i class="fa-solid fa-trash-can"></i>' +
                    '</button>' +
                '</div>' +
            '</td>' + 
        '</tr>';   // סוגר טבלה
     }

    // עדכון הטבלה במסך
    document.getElementById('usersTableBody').innerHTML = rowsHtml;

    // עדכון הריבועים למעלה
    document.getElementById('totalUsersCount').textContent = totalCount;
    document.getElementById('verifiedUsersCount').textContent = verifiedCount;
    document.getElementById('pendingUsersCount').textContent = pendingCount;
    document.getElementById('tableCountText').textContent = 'מציג ' + totalCount + ' משתמשים';  // מציג 1 משתמשים בצד שמאל הטבלה
}

// פונקציה למחיקת משתמש לפי  ID
async function deleteUser(userId) 
{
    // שואלים את המנהל אם הוא בטוח
    const confirmResult = await Swal.fire({
        title: 'מחיקת משתמש מהמערכת',
        text: 'האם אתה בטוח שברצונך למחוק משתמש זה לצמיתות? פעולה זו היא סופית ובלתי הפיכה, ותסיר את כל נתוני החשבון ממאגר המידע.',
        icon: 'warning',
        iconColor: '#ef4444',
        showCancelButton: true,
        confirmButtonColor: '#ef4444', // צבע אדום של מחיקה
        cancelButtonColor: '#94a3b8',
        confirmButtonText: 'כן, מחק משתמש',
        cancelButtonText: 'ביטול'
    });

    // אם המנהל אישר
    if (confirmResult.isConfirmed === true) 
    {
        // שליפת הטוקן של המנהל מהזיכרון של הדפדפן
        const authToken = localStorage.getItem('authToken');

        // שליחת בקשה לשרת למחיקת המשתמש עם הטוקן
        const response = await fetch(API_URL + '/api/admin/delete-user', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ id: userId, token: authToken })
        });

         // בדיקה אם הייתה שגיאה בשרת
        if (response.ok === false) 
        {
            Swal.fire({
                title: 'פעולת המחיקה נכשלה',
                text: 'אירעה שגיאה זמנית בעת הניסיון להסיר את המשתמש מהמערכת. אנא נסה שוב מאוחר יותר.',
                icon: 'error',
                confirmButtonColor: '#0a4ade',
                confirmButtonText: 'אישור'
            });
            return;
        }

         const message = await response.text(); // שמירת התשובה

        // בדיקה אם הגישה נדחתה בגלל חוסר הרשאות מנהל
        if (message.includes('גישה נדחתה') === true) 
        {
            Swal.fire({
                title: 'שגיאת אבטחה',
                text: message,
                icon: 'error',
                confirmButtonColor: '#0a4ade',
                confirmButtonText: 'אישור'
            });
            return;
        }

        // הצגת הודעת הצלחה
        await Swal.fire({
            title: 'הפעולה בוצעה בהצלחה',
            text: message,
            icon: 'success',
            confirmButtonColor: '#0a4ade',
            confirmButtonText: 'המשך'
        });

        // רענון הטבלה והסטטיסטיקות מיד
        loadUsers();
        loadLastAction();
    }
}

// חיפוש בטבלה
function searchUsers() 
{
    // מה שהמשתמש הקליד
    const searchText = document.getElementById('searchInput').value.toLowerCase();
    
    // גוף הטבלה והשורות שבתוכו
    const tableBody = document.getElementById('usersTableBody');
    const rows = tableBody.rows;
    let visibleCount = 0; //  משתנה לכמה שורות נשארו גלויות

    // לולאה שעוברת שורה שורה
    for (let i = 0; i < rows.length; i++) 
    {
        const rowText = rows[i].textContent.toLowerCase();

        // אם המילה קיימת בשורה מציגים, אם לא מסתירים
        if (rowText.includes(searchText) === true) 
        {
            rows[i].style.display = ''; // מציג
            visibleCount++; // מוסיפים 1 לספירה של מי שרואים    
        } 
        else 
        {   
            rows[i].style.display = 'none'; // מסתיר
        }
            // עדכון המספר בטבלה בזמן אמת
    document.getElementById('tableCountText').textContent = 'מציג ' + visibleCount + ' משתמשים';
    }
}

// פונקציה לעריכת משתמש שמקבלת אובייקט של המשתמש
async function editUser(user) 
{
    // פתיחת חלון העריכה
    const formResult = await Swal.fire({
        title: 'עריכת פרטי משתמש',
        html:
            '<div class="edit-modal-form">' +
                '<div class="edit-form-group">' +
                    '<label>שם מלא:</label>' +
                    '<input id="swalFullName" class="edit-modal-input" placeholder="הזן שם מלא" value="' + user.full_name + '">' +
                '</div>' +
                '<div class="edit-form-group">' +
                    '<label>שם משתמש:</label>' +
                    '<input id="swalUsername" class="edit-modal-input" placeholder="הזן שם משתמש" value="' + user.username + '">' +
                '</div>' +
                '<div class="edit-form-group">' +
                    '<label>כתובת אימייל:</label>' +
                    '<input id="swalEmail" type="email" class="edit-modal-input" placeholder="הזן כתובת אימייל" value="' + user.email + '">' +
                '</div>' +
            '</div>',
        showCancelButton: true,
        confirmButtonColor: '#0a4ade',
        cancelButtonColor: '#94a3b8',
        confirmButtonText: 'שמור שינויים',
        cancelButtonText: 'ביטול',
        preConfirm: function() 
        {
            return {
                id: user.id,
                full_name: document.getElementById('swalFullName').value.trim(), // מנקה רווחים מהצדדים 
                username: document.getElementById('swalUsername').value.trim(),
                email: document.getElementById('swalEmail').value.trim(),
            };
        }
    });

    // אם המנהל אישר את השינויים
    if (formResult.isConfirmed === true) 
    {
        const updatedData = formResult.value;

        // הוספת הטוקן של המנהל לנתונים שנשלחים לשרת
        const authToken = localStorage.getItem('authToken');
        updatedData.token = authToken;

        // שליחת הנתונים המעודכנים לשרת
        const response = await fetch(API_URL + '/api/admin/update-user', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(updatedData)
        });

        // בדיקה אם הייתה שגיאה בשרת
        if (response.ok === false) 
        {
            Swal.fire({
                title: 'שגיאה בשמירת השינויים',
                text: 'אירעה שגיאה זמנית בעת שמירת השינויים במערכת, אנא נסה שוב בעוד מספר רגעים.',
                icon: 'error',
                confirmButtonColor: '#0a4ade',
                confirmButtonText: 'אישור'
            });
            return;
        }

        const message = await response.text(); // שמירת התשובה

        // בדיקה אם הגישה נדחתה בגלל חוסר הרשאות מנהל
        if (message.includes('גישה נדחתה') === true) 
        {
            Swal.fire({
                title: 'שגיאת אבטחה',
                text: message,
                icon: 'error',
                confirmButtonColor: '#0a4ade',
                confirmButtonText: 'אישור'
            });
            return;
        }

        // הודעת הצלחה
        await Swal.fire({
            title: 'העדכון בוצע בהצלחה',
            text: message,
            icon: 'success',
            confirmButtonColor: '#0a4ade',
            confirmButtonText: 'המשך'
        });

        // רענון הטבלה והפעולה האחרונה
        loadUsers();
        loadLastAction();
    }
}

// פונקציה שמנקה תווים מסוכנים ומחזירה טקסט בטוח להצגה
function safeText(text) 
{
    // בדיקה אם הטקסט ריק או לא קיים
    if (text === null || text === undefined) 
    {
        return '';
    }

    // יצירת אלמנט זמני בזיכרון של הדפדפן
    const div = document.createElement('div');

    // הכנסה מאובטחת שהופכת תווים מסוכנים לטקסט בלבד
    div.textContent = text;

    // מחזיר את הטקסט כאשר התווים המסוכנים מנוטרלים ומומרים לקוד בטוח
    return div.innerHTML;
}