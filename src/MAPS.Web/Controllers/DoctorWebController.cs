using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAPS.Web.Controllers;

[Authorize(Roles = "Doctor")]
[Route("doctor")]
public class DoctorWebController : Controller
{
    // GET /doctor/dashboard
    [HttpGet("dashboard")]
    public IActionResult Dashboard() => View();

    // GET /doctor/patients
    [HttpGet("patients")]
    public IActionResult Patients() => View();

    // GET /doctor/patients/{id}
    [HttpGet("patients/{id:guid}")]
    public IActionResult PatientDetail(Guid id)
    {
        ViewBag.PatientId = id;
        return View();
    }

    // GET /doctor/prescriptions/create/{patientId}
    [HttpGet("prescriptions/create/{patientId:guid}")]
    public IActionResult CreatePrescription(Guid patientId)
    {
        ViewBag.PatientId = patientId;
        return View();
    }

    // GET /doctor/notes/{patientId}
    [HttpGet("notes/{patientId:guid}")]
    public IActionResult ClinicalNotes(Guid patientId)
    {
        ViewBag.PatientId = patientId;
        return View();
    }
}
