// LHCForms JavaScript Interop
window.lformsHelper = {
    renderQuestionnaireResponse: function (containerId, questionnaireJson, questionnaireResponseJson) {
        try {
            const container = document.getElementById(containerId);
            if (!container) {
                console.error('Container not found:', containerId);
                return false;
            }

            // Clear any existing content
            container.innerHTML = '';

            // Check if LForms is available
            if (!window.LForms || !window.LForms.Util) {
                console.error('LForms library not loaded.');
                container.innerHTML = '<div class="alert alert-danger">LForms library not loaded. Please refresh the page.</div>';
                return false;
            }

            try {
                // Parse the inputs
                const questionnaire = JSON.parse(questionnaireJson);
                const qr = JSON.parse(questionnaireResponseJson);

                console.log('Rendering form with LForms...');

                // 1. Convert FHIR Questionnaire to LForms format
                // The library expects 'R4' as the second argument for version
                let formData = window.LForms.Util.convertFHIRQuestionnaireToLForms(questionnaire, 'R4');

                // 2. Merge in the QuestionnaireResponse data
                if (qr && qr.item) {
                    const mergedData = window.LForms.Util.mergeFHIRDataIntoLForms('QuestionnaireResponse', qr, formData, 'R4');
                    formData = mergedData; // Update formData with the result
                }

                // 3. Render the form
                window.LForms.Util.addFormToPage(formData, containerId, {
                    prepopulate: true,      // Tells LForms to populate with merged data
                    displayInstructions: true,
                    noSubmit: true          // Hide submit button since we are just viewing
                });

                // 4. Make inputs read-only after rendering (optional polish)
                setTimeout(() => {
                    const inputs = container.querySelectorAll('input, textarea, select');
                    inputs.forEach(input => {
                        input.setAttribute('readonly', 'readonly');
                        input.setAttribute('disabled', 'disabled');
                    });
                }, 200);

                return true;
            } catch (err) {
                console.error('LForms processing error:', err);
                container.innerHTML = `<div class="alert alert-danger">Error processing form: ${err.message}</div>`;
                return false;
            }
        } catch (error) {
            console.error('Error rendering QuestionnaireResponse:', error);
            const container = document.getElementById(containerId);
            if (container) {
                container.innerHTML = `<div class="alert alert-danger">Error: ${error.message}</div>`;
            }
            return false;
        }
    },

    clearContainer: function (containerId) {
        const container = document.getElementById(containerId);
        if (container) {
            container.innerHTML = '';
        }
    }
};

console.log('LForms helper loaded');
