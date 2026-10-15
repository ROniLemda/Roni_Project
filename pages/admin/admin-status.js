// כתובת שרת ה-API של המערכת
const API_URL = 'http://localhost:5057';

// בעת טעינת הדף
window.onload = function() 
{
    // חיבור כפתור ההתנתקות של המנהל
    const logoutBtn = document.getElementById('adminLogoutBtn');
    if (logoutBtn) 
    {
        logoutBtn.onclick = logoutAdmin;
    }
};

// פונקציית התנתקות מנהל
async function logoutAdmin() 
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
        localStorage.clear();
        window.location.replace('../login.html');
    }
}
