import React from "react";
import { remote } from "services/remote";

class ErrorBoundary extends React.Component {

    constructor(props) {
        super(props);
        this.state = { hasError: false };
    }

    static getDerivedStateFromError() {
        return { hasError: true };
    }

    componentDidCatch(error, errorInfo) {
        remote.unhandledException(error + '\n\n' + JSON.stringify(errorInfo));
    }

    render() {
        if (this.state.hasError) {
            return <></>;
        }
        return this.props.children;
    }
}

export default ErrorBoundary