// כתובת השרת
const API_URL = 'http://localhost:5057';

window.onload = async function() 
{
    // בדיקה אם יש משתמש שמור בזיכרון של הדפדפן
    const savedUser = localStorage.getItem('currentUser');

    // אם לא מחובר זורק אותו להתחברות
    if (savedUser === null) 
    {
        window.location.href = 'login.html';
        return;
    }

    // שולפים את הטוקן מזיכרון הדפדפן
    const authToken = localStorage.getItem('authToken');

    // בדיקה אם אין טוקן מעיפים מיד להתחברות
    if (authToken === null || authToken === '') 
    {
        window.location.replace('login.html');
        return;
    }

    // אבטחה מול השרת בודקים שהטוקן באמת קיים במסד הנתונים ולא זויף בקונסול
    const response = await fetch(API_URL + '/api/verify-token', 
    {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: authToken })
    });

    const result = await response.text();
    // אם השרת לא החזיר שהטוקן תקין (אז האקר / טוקן פג תוקף) מנקים זיכרון ומעיפים
    if (result.includes('אינו תקין') === true)
    {
        localStorage.removeItem('authToken');
        localStorage.removeItem('currentUser');
        window.location.replace('login.html');
        return;
    }

    //  אם הגענו לפה אז תקין השרת מחזיר: את התשובה (תקין) | שם |מייל מפרקים ומציגים לפי הסדר
    const parts = result.split('|');
    const fullName = parts[1];
    const userEmail = parts[2];

    // מעדכן את השם משתמש והשם פרטי בדשבורד (בסרגל הצדדי ובטופ בר העליון)
    document.getElementById('userNameDisplay').textContent = fullName;
    document.getElementById('userEmailDisplay').textContent = userEmail;
    document.getElementById('topUserNameDisplay').textContent = fullName;

    // שעה נוכחית (0 עד 23)
    const hour = new Date().getHours();
    let title = '';
    let text = '';

    // בדיקה לפי השעה
    if (hour >= 5 && hour < 12) 
    {
        title = 'בוקר טוב, ' + fullName;
        text = 'שמחים לראותך שוב בסביבת העבודה שלך לוח הבקרה מעודכן ומוכן להמשך ניהול ומעקב המסמכים שלך.';
    } 
    else if (hour >= 12 && hour < 18) 
    {
        title = 'צהריים טובים, ' + fullName;
        text = 'ברוך שובך למערכת חתימה+ כל הנתונים הדיווחים והפעולות הנדרשות שלך סונכרנו בהצלחה וממתינים לך לניהול.';
    } 
    else 
    {
        title = 'ערב טוב, ' + fullName;
        text = 'ברוך שובך למערכת חתימה+ כל הנתונים הדיווחים והפעולות הנדרשות שלך סונכרנו בהצלחה וממתינים לך בלוח המעקב הראשי.';
    }

    // מציגים על המסך
    document.getElementById('welcomeTitle').textContent = title;
    document.getElementById('welcomeSubtitle').textContent = text;

    
    const logoutBtn = document.getElementById('logoutBtn'); // כפתור ההתנתקות 
    logoutBtn.onclick = async function() 
    {
        const confirmResult = await Swal.fire({
            title:'התנתקות מהמערכת',
            text: 'לצורך שמירה על אבטחת המידע, האם אתה בטוח שברצונך לצאת מחשבונך ולבצע התנתקות מהחשבון?',
            icon:'question',
            showCancelButton: true,
            confirmButtonColor: '#0a4ade',
            cancelButtonColor: '#94a3b8',
            confirmButtonText: 'אישור ויציאה',
            cancelButtonText: 'ביטול'
        });

        // אם המשתמש לחץ על כפתור אישור ויציאה
        if (confirmResult.isConfirmed === true) 
        {
            // שליחת בקשה לשרת למחיקת הטוקן ממסד הנתונים
            await fetch(API_URL + '/api/logout', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ token: authToken })
            });

            // מנקים את כל הנתונים של המשתמש מהדפדפן
            localStorage.removeItem('authToken');
            localStorage.removeItem('currentUser');
            // מעבירים חזרה למסך ההתחברות
            window.location.replace('login.html');
        }
    };

    // חיבור כפתור ההתנתקות שבתפריט הפרופיל העליון (טופ בר) לאותה פעולת התנתקות בדיוק
    const topLogoutBtn = document.getElementById('topLogoutBtn');
    if (topLogoutBtn) 
    {
        topLogoutBtn.onclick = logoutBtn.onclick; //logoutBtn יחול גם על השני מעכשיו כל פעולה על 
    }


    // שולפים את 4 הכפתורים של התפריט הצדדי 
    const btnDocs = document.getElementById('menuDocs');
    const btnReports = document.getElementById('menuReports');
    const btnTasks = document.getElementById('menuTasks');
    const btnSigners = document.getElementById('menuSigners');

    // שמים את כולם במערך 
    const allButtons = [btnDocs, btnReports, btnTasks, btnSigners];

    // פונקציה פשוטה שסוגרת את כל התפריטים עם לולאת for רגילה
    function closeAllMenus() 
    {
        for (let i = 0; i < allButtons.length; i++) 
        {
            allButtons[i].classList.remove('open');
        }
    }

    // לחיצה על ניהול מסמכים
    btnDocs.onclick = function(e) 
    {
        e.preventDefault(); // מונע ריפרש אוטומט 
        const isOpen = btnDocs.classList.contains('open');
        closeAllMenus();
        if (isOpen === false) 
        {
            btnDocs.classList.add('open');
        }
    };

    // לחיצה על דוחות
    btnReports.onclick = function(e) 
    {
        e.preventDefault();
        const isOpen = btnReports.classList.contains('open'); // פעולה שבודקת אם open קיים
        closeAllMenus();
        if (isOpen === false) 
        {
            btnReports.classList.add('open');
        }
    };

    // לחיצה על פעולות נדרשות
    btnTasks.onclick = function(e) 
    {
        e.preventDefault();
        const isOpen = btnTasks.classList.contains('open');
        closeAllMenus();
        if (isOpen === false) 
        {
            btnTasks.classList.add('open');
        }
    };

    // לחיצה על חותמים ואנשים מורשים
    btnSigners.onclick = function(e) 
    {
        e.preventDefault();
        const isOpen = btnSigners.classList.contains('open');
        closeAllMenus();
        if (isOpen === false) 
        {
            btnSigners.classList.add('open');
        }
    };

    // מעדכנים את השם והאימייל גם בתוך תיבת הפרופיל הצפה
    document.getElementById('boxName').textContent = fullName;
    document.getElementById('boxEmail').textContent = userEmail;

    // לחיצה על הפרופיל בטופ בר פותחת וסוגרת את תפריט המשתמש
    const profileBox = document.getElementById('topProfileContainer');
    profileBox.onclick = function(e) 
    {
        e.stopPropagation(); // מונע סגירה מיידית
        profileBox.classList.toggle('open');
    };

    // סגירת התפריט בלחיצה בכל מקום אחר במסך
    document.addEventListener('click', function() 
    {
        profileBox.classList.remove('open');
    });

};
