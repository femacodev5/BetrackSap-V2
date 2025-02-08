const es=new Intl.RelativeTimeFormat("es-pe"),DATE_UNITS={month: 2592e3,week: 604800,day: 86400,hour: 3600,minute: 60,second: 1},
    fDate=new Intl.DateTimeFormat('en-gb'),fNum=Intl.NumberFormat('en',{minimumFractionDigits: 2,maximumFractionDigits: 2}),sToD=s => fDate.format(new Date(s)),nToS=n => fNum.format(n),

    intVal=(i) => typeof i==='string'? i.replace(/[\$,]/g,'')*1:typeof i==='number'? i:0;
    //sToD=(d) => new Date(d).toLocaleDateString('en-gb'),//
    //sToDH=s => new Date(s).toLocaleString('en-gb',{dateStyle: 'short',timeStyle: 'short',hour12: true});
    //=(n,dec=2) => (n??'').toLocaleString('en',{minimumFractionDigits: dec,maximumFractionDigits: dec}),

const loadBtn=(b) => b? (b.insertAdjacentHTML('afterbegin','<i class="spinner-border spinner-border-sm"></i> '),b.nodeName=='A'? b.classList.add('disabled'):b.disabled=true)
    : (document.querySelector('.spinner-border').parentNode.disabled = false || document.querySelector('.spinner-border').parentNode.classList.remove('disabled'), document.querySelector('.spinner-border').remove());
const Toast=Swal.mixin({toast:true,position:'top-end',showConfirmButton:false,timer:3000,timerProgressBar:true});
const ellipsis=(cutoff) => (d,t,r) => {
    if(t!=='display') {return d;} if(typeof d!=='number'&&typeof d!=='string') {return d;} d=d.toString();//cast numbers
    if(d.length<=cutoff) {return d;} let shortened=d.substr(0,cutoff-1);
    return '<span class="ellipsis" title="'+d+'">'+shortened+'&#8230;</span>';
}