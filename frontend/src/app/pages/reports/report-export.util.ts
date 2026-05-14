import * as XLSX from 'xlsx';
import { jsPDF } from 'jspdf';
import autoTable from 'jspdf-autotable';

export function exportToExcel(filename: string, sheetName: string, rows: Record<string, unknown>[]): void {
  const ws = XLSX.utils.json_to_sheet(rows);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, sheetName.slice(0, 31));
  XLSX.writeFile(wb, filename.endsWith('.xlsx') ? filename : `${filename}.xlsx`);
}

export function exportToPdf(
  title: string,
  filename: string,
  head: string[][],
  body: (string | number)[][]
): void {
  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' });
  doc.setFontSize(14);
  doc.text(title, 14, 16);
  autoTable(doc, {
    head,
    body,
    startY: 22,
    styles: { fontSize: 8 },
    headStyles: { fillColor: [36, 87, 214] }
  });
  doc.save(filename.endsWith('.pdf') ? filename : `${filename}.pdf`);
}
